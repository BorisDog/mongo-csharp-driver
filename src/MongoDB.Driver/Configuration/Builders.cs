/* Copyright 2010-present MongoDB Inc.
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
using System.Text;
using MongoDB.Driver.Core.Compression;
using MongoDB.Driver.Core.Servers;

namespace MongoDB.Driver.Builders
{
    /// <summary>
    /// Compress settings
    /// </summary>
    public readonly record struct Compressor
    {
        /// <summary>
        /// Gets the compressor type.
        /// </summary>
        public CompressorType Type { get; init; }

        /// <summary>
        /// Gets the compressor properties.
        /// </summary>
        public IReadOnlyDictionary<string, object> Properties { get; init; }
    }

    /// <summary>
    /// The serialization settings.
    /// </summary>
    public readonly record struct SerializationSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SerializationSettings"/> record.
        /// </summary>
        public SerializationSettings()
        {
            ReadEncoding = null;
            WriteEncoding = null;
        }

        /// <summary>
        /// Gets the read encoding.
        /// </summary>
        public UTF8Encoding ReadEncoding { get; init; }

        /// <summary>
        /// Gets the write encoding.
        /// </summary>
        public UTF8Encoding WriteEncoding { get; init; }

        // TBD serialization settings, serializers, class maps...
    }

    /// <summary>
    /// The server settings.
    /// </summary>
    public readonly struct ServerSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ServerSettings"/> record.
        /// </summary>
        public ServerSettings()
        {
            ConnectTimeout = MongoInternalDefaults.ServerSettings.ConnectTimeout;
            HeartbeatInterval = MongoInternalDefaults.ConnectionPool.HeartbeatInterval;
            HeartbeatTimeout = MongoInternalDefaults.ConnectionPool.HeartbeatTimeout;
            LocalThreshold = MongoInternalDefaults.ServerSettings.LocalThreshold;
            ServerMonitoringMode = MongoInternalDefaults.ServerSettings.ServerMonitoringMode;
            ServerSelectionTimeout = MongoInternalDefaults.ServerSettings.ServerSelectionTimeout;
            ServerApi = null;
            Servers = new List<MongoServerAddress> { new MongoServerAddress("localhost") }; ;
            SocketTimeout = MongoInternalDefaults.ServerSettings.SocketTimeout;
            SrvMaxHosts = MongoInternalDefaults.ServerSettings.SrvMaxHosts;
            SrvServiceName = MongoInternalDefaults.ServerSettings.SrvServiceName;
            SslSettings = null;
            UseTls = MongoInternalDefaults.ServerSettings.UserTls;
        }

        /// <summary>
        /// Gets the TCP socket connection timeout.
        /// </summary>
        public TimeSpan ConnectTimeout { get; init; }

        /// <summary>
        /// Gets the heartbeat interval.
        /// </summary>
        public TimeSpan HeartbeatInterval { get; init; }

        /// <summary>
        /// Gets the heartbeat timeout.
        /// </summary>
        public TimeSpan HeartbeatTimeout { get; init; }

        /// <summary>
        /// Gets the local threshold.
        /// Maximal server latency windows for selection.
        /// </summary>
        public TimeSpan LocalThreshold { get; init; }

        /// <summary>
        /// Gets the server monitoring mode.
        /// </summary>
        public ServerMonitoringMode ServerMonitoringMode { get; init; }

        /// <summary>
        /// Gets the server selection timeout.
        /// </summary>
        public TimeSpan ServerSelectionTimeout { get; init; }

        /// <summary>
        /// Gets the server api.
        /// </summary>
        public ServerApi ServerApi { get; init; }

        /// <summary>
        /// Gets the servers.
        /// </summary>
        public IReadOnlyList<MongoServerAddress> Servers { get; init; }

        /// <summary>
        /// Gets the socket send or receive timeout.
        /// </summary>
        public TimeSpan SocketTimeout { get; init; }

        /// <summary>
        /// Limits the number of SRV records used to populate the seedlist
        /// during initial discovery, as well as the number of additional hosts
        /// that may be added during SRV polling.
        /// </summary>
        public int SrvMaxHosts { get; init; }

        /// <summary>
        /// Gets or sets the SRV service name which modifies the srv URI to look like:
        /// <code>_{srvServiceName}._tcp.{hostname}.{domainname}</code>
        /// The default value is "mongodb".
        /// </summary>
        public string SrvServiceName { get; init; }

        /// <summary>
        /// Gets the SSL settings.
        /// </summary>
        public SslSettings SslSettings { get; init; }

        /// <summary>
        /// Gets a value indicating whether use TLS for connections to the server.
        /// </summary>
        public bool UseTls { get; init; }
    }

    /// <summary>
    /// The connection pool settings.
    /// </summary>
    public readonly record struct ConnectionPoolSettings
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ConnectionPoolSettings"/> record.
        /// </summary>
        public ConnectionPoolSettings()
        {
            MaxConnecting = MongoInternalDefaults.ConnectionPool.MaxConnecting;
            MaxConnectionIdleTime = MongoInternalDefaults.ConnectionPool.MaxConnectionIdleTime;
            MaxConnectionLifeTime = MongoInternalDefaults.ConnectionPool.MaxConnectionLifeTime;
            MaxConnectionPoolSize = MongoInternalDefaults.ConnectionPool.MaxConnectionPoolSize;
            MinConnectionPoolSize = MongoInternalDefaults.ConnectionPool.MinConnectionPoolSize;
            WaitQueueTimeout = MongoInternalDefaults.ConnectionPool.WaitQueueTime;
        }  

        /// <summary>
        /// Gets the max connecting.
        /// </summary>
        public int MaxConnecting { get; init; }

        /// <summary>
        /// Gets the max connection idle time.
        /// </summary>
        public TimeSpan MaxConnectionIdleTime { get; init; }

        /// <summary>
        /// Gets the max connection life time.
        /// </summary>
        public TimeSpan MaxConnectionLifeTime { get; init; }

        /// <summary>
        /// Gets the max connection pool size.
        /// </summary>
        public int MaxConnectionPoolSize { get; init; }

        /// <summary>
        /// Gets the min connection pool size.
        /// </summary>
        public int MinConnectionPoolSize { get; init; }

        /// <summary>
        /// Gets the wait queue timeout.
        /// </summary>
        public TimeSpan WaitQueueTimeout { get; init; }
    }
}
