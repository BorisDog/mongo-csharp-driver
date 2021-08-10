﻿/* Copyright 2021-present MongoDB Inc.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
* http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using MongoDB.Bson;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.ConnectionPools;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Misc;

namespace MongoDB.Driver.Core.Servers
{
    internal class DefaultServer : Server
    {
        #region static
        // static fields
        private static readonly List<Type> __invalidatingExceptions = new List<Type>
        {
            typeof(MongoConnectionException),
            typeof(SocketException),
            typeof(EndOfStreamException),
            typeof(IOException),
        };
        #endregion

        private readonly ServerDescription _baseDescription;
        private volatile ServerDescription _currentDescription;
        private readonly IServerMonitor _monitor;

        public DefaultServer(
            ClusterId clusterId,
            IClusterClock clusterClock,
#pragma warning disable CS0618 // Type or member is obsolete
            ClusterConnectionMode clusterConnectionMode,
            ConnectionModeSwitch connectionModeSwitch,
#pragma warning restore CS0618 // Type or member is obsolete
            bool? directConnection,
            ServerSettings settings,
            EndPoint endPoint,
            IConnectionPoolFactory connectionPoolFactory,
            IServerMonitorFactory monitorFactory,
            IEventSubscriber eventSubscriber,
            ServerApi serverApi)
            : base(
                  clusterId,
                  clusterClock,
                  clusterConnectionMode,
                  connectionModeSwitch,
                  directConnection,
                  settings,
                  endPoint,
                  connectionPoolFactory,
                  eventSubscriber,
                  serverApi)
        {
            _monitor = Ensure.IsNotNull(monitorFactory, nameof(monitorFactory)).Create(ServerId, endPoint);
            _baseDescription = _currentDescription = new ServerDescription(ServerId, endPoint, reasonChanged: "ServerInitialDescription", heartbeatInterval: settings.HeartbeatInterval);
        }

        // properties
        public override ServerDescription Description => _currentDescription;

        // public methods
        public override void Invalidate(string reasonInvalidated, bool clearConnectionPool, TopologyVersion topologyVersion)
        {
            if (clearConnectionPool)
            {
                ConnectionPool.Clear();
            }
            var newDescription = _baseDescription.With(
                    $"InvalidatedBecause:{reasonInvalidated}",
                    lastUpdateTimestamp: DateTime.UtcNow,
                    topologyVersion: topologyVersion);
            SetDescription(newDescription);
            // TODO: make the heartbeat request conditional so we adhere to this part of the spec
            // > Network error when reading or writing: ... Clients MUST NOT request an immediate check of the server;
            // > since application sockets are used frequently, a network error likely means the server has just become
            // > unavailable, so an immediate refresh is likely to get a network error, too.
            RequestHeartbeat();
        }

        public override void RequestHeartbeat()
        {
            _monitor.RequestHeartbeat();
        }

        // protected methods
        protected override void Dispose(bool disposing)
        {
            _monitor.Dispose();
            _monitor.DescriptionChanged -= OnMonitorDescriptionChanged;
        }

        protected override void HandleBeforeHandshakeCompletesException(Exception ex)
        {
            if (ex is not MongoConnectionException connectionException)
            {
                // non connection exception
                return;
            }

            var (invalidateAndClear, cancelCheck) = ex switch
            {
                MongoAuthenticationException => (invalidateAndClear: true, cancelCheck: false),
                _ => (invalidateAndClear: connectionException.IsNetworkException || connectionException.ContainsTimeoutException,
                      cancelCheck: connectionException.IsNetworkException && !connectionException.ContainsTimeoutException)
            };

            if (invalidateAndClear)
            {
                lock (_monitor.Lock)
                {
                    if (connectionException.Generation != null && connectionException.Generation != ConnectionPool.Generation)
                    {
                        // stale generation number
                        return;
                    }

                    if (cancelCheck)
                    {
                        _monitor.CancelCurrentCheck();
                    }

                    Invalidate($"ChannelException during handshake: {ex}.", clearConnectionPool: true, topologyVersion: null);
                }
            }
        }

        protected override void HandleAfterHandshakeCompletesException(IConnection connection, Exception ex)
        {
            lock (_monitor.Lock)
            {
                if (ex is MongoConnectionException mongoConnectionException)
                {
                    if (mongoConnectionException.Generation != null &&
                        mongoConnectionException.Generation != ConnectionPool.Generation)
                    {
                        return; // stale generation number
                    }

                    if (mongoConnectionException.IsNetworkException &&
                        !mongoConnectionException.ContainsTimeoutException)
                    {
                        _monitor.CancelCurrentCheck();
                    }
                }

                var description = Description; // use Description property to access _description value safely
                if (ShouldInvalidateServer(connection, ex, description, out TopologyVersion responseTopologyVersion))
                {
                    var shouldClearConnectionPool = ShouldClearConnectionPoolForChannelException(ex, connection.Description.ServerVersion);
                    Invalidate($"ChannelException:{ex}", shouldClearConnectionPool, responseTopologyVersion);
                }
                else
                {
                    RequestHeartbeat();
                }
            }
        }

        protected override void InitializeSubClass()
        {
            _monitor.DescriptionChanged += OnMonitorDescriptionChanged;
            _monitor.Initialize();
        }

        // private methods
        private void OnMonitorDescriptionChanged(object sender, ServerDescriptionChangedEventArgs e)
        {
            var currentDescription = _currentDescription;

            var heartbeatException = e.NewServerDescription.HeartbeatException;
            // The heartbeat commands are hello (or legacy hello) + buildInfo. These commands will throw a MongoCommandException on
            // {ok: 0}, but a reply (with a potential topologyVersion) will still have been received.
            // Not receiving a reply to the heartbeat commands implies a network error or a "HeartbeatFailed" type
            // exception (i.e. ServerDescription.WithHeartbeatException was called), in which case we should immediately
            // set the description to "Unknown"// (which is what e.NewServerDescription will be in such a case)
            var heartbeatReplyNotReceived = heartbeatException != null && !(heartbeatException is MongoCommandException);

            // We cannot use FresherThan(e.NewServerDescription.TopologyVersion, currentDescription.TopologyVersion)
            // because due to how TopologyVersions comparisons are defined, IsStalerThanOrEqualTo(x, y) does not imply
            // FresherThan(y, x)
            if (heartbeatReplyNotReceived ||
                TopologyVersion.IsStalerThanOrEqualTo(currentDescription.TopologyVersion, e.NewServerDescription.TopologyVersion))
            {
                SetDescription(e.NewServerDescription);
            }
        }

        private void SetDescription(ServerDescription newDescription)
        {
            var oldDescription = _currentDescription;

            if (newDescription.HeartbeatException != null)
            {
                // set new description before clearing the pool
                _currentDescription = newDescription;

                ConnectionPool.Clear();
            }
            else
            {
                if (newDescription.IsDataBearing ||
                    (newDescription.Type != ServerType.Unknown && IsDirectConnection()))
                {
                    // The spec requires to check (server.type != Unknown and newTopologyDescription.type == Single)
                    // in C# driver servers in single topology will be only selectable if direct connection was requested
                    // therefore it is sufficient to check whether the connection mode is directConnection.

                    ConnectionPool.SetReady();
                }

                // set new description after marking pool as ready
                _currentDescription = newDescription;
            }

            var descriptionChangedEvent = new ServerDescriptionChangedEventArgs(oldDescription, newDescription);

            // propagate event to upper levels
            TriggerServerDescriptionChanged(this, descriptionChangedEvent);
        }

        private bool ShouldInvalidateServer(
            IConnection connection,
            Exception exception,
            ServerDescription description,
            out TopologyVersion invalidatingResponseTopologyVersion)
        {
            if (exception is MongoConnectionException mongoConnectionException &&
                mongoConnectionException.ContainsTimeoutException)
            {
                invalidatingResponseTopologyVersion = null;
                return false;
            }

            if (__invalidatingExceptions.Contains(exception.GetType()))
            {
                invalidatingResponseTopologyVersion = null;
                return true;
            }

            var exceptionsToCheck = new[]
            {
                exception as MongoCommandException,
                (exception as MongoWriteConcernException)?.MappedWriteConcernResultException
            }
            .OfType<MongoCommandException>();
            foreach (MongoCommandException commandException in exceptionsToCheck)
            {
                if (IsStateChangeException(commandException))
                {
                    return !IsStaleStateChangeError(commandException.Result, out invalidatingResponseTopologyVersion);
                }
            }

            invalidatingResponseTopologyVersion = null;
            return false;

            bool IsStaleStateChangeError(BsonDocument response, out TopologyVersion nonStaleResponseTopologyVersion)
            {
                if (ConnectionPool.Generation > connection.Generation)
                {
                    // stale generation number
                    nonStaleResponseTopologyVersion = null;
                    return true;
                }

                var responseTopologyVersion = TopologyVersion.FromMongoCommandResponse(response);
                // We use FresherThanOrEqualTo instead of FresherThan because a state change should come with a new
                // topology version.
                // We cannot use StalerThan(responseTopologyVersion, description.TopologyVersion) because due to how
                // TopologyVersions comparisons are defined, FresherThanOrEqualTo(x, y) does not imply StalerThan(y, x)
                bool isStale = TopologyVersion.IsFresherThanOrEqualTo(description.TopologyVersion, responseTopologyVersion);

                nonStaleResponseTopologyVersion = isStale ? null : responseTopologyVersion;
                return isStale;
            }
        }
    }
}
