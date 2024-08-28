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

using System.Collections.Generic;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Configuration;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;

namespace MongoDB.Driver
{
    internal interface IMongoClientOptions
    {
        public bool AllowInsecureTls { get; }
        public string ApplicationName { get; }
        public IClusterSource ClusterSource { get; }
        public IReadOnlyList<Compressor> Compressors { get; }
        public Builders.ConnectionPoolSettings ConnectionPoolSettings { get; }
        public IReadOnlyList<IEventSubscriber> EventSubscribers { get; }
        public ConnectionStringScheme Scheme { get; }
        public string ReplicaSetName { get; }
        public MongoCredential Credential { get; }
        public bool? DirectConnection { get; }
        public bool IPv6 { get; init; }
        public LibraryInfoOptions LibraryInfoOptions { get; }
        public bool LoadBalanced { get; }
        public LoggingOptions LoggingOptions { get; }
        public ReadConcern ReadConcern { get; }
        public ReadPreference ReadPreference { get; }
        public bool RetryReads { get; }
        public bool RetryWrites { get; }
        public SerializationSettings SerializationSettings { get; }
        public Builders.ServerSettings ServerSettings { get; }
        public WriteConcern WriteConcern { get; init; }
    }

    internal static class MongoClientSettingsExtensions
    {
        internal IMongoClientOptions ToIMongoClientOptions(this MongoClientSettings mongoClientSettings)
        {
            var builder = new MongoClientBuilder()
            {
                AllowInsecureTls = mongoClientSettings.AllowInsecureTls,
                ApplicationName = mongoClientSettings.ApplicationName,
                Compressors = Compressors,
                ConnectionPoolSettings = new Driver.Builders.ConnectionPoolSettings()
                {
                    MaxConnecting = mongoClientSettings.MaxConnecting,
                    MaxConnectionIdleTime = mongoClientSettings.MaxConnectionIdleTime,
                    MaxConnectionLifeTime = mongoClientSettings.MaxConnectionLifeTime,
                    MaxConnectionPoolSize = mongoClientSettings.MaxConnectionPoolSize,
                    MinConnectionPoolSize = mongoClientSettings.MinConnectionPoolSize,
                    WaitQueueTimeout = mongoClientSettings.WaitQueueTimeout,
                },
                //Credential
                DirectConnection = mongoClientSettings.DirectConnection,
                //EventSubscribers = _
                IPv6 = mongoClientSettings.IPv6,
                LibraryInfoOptions = new LibraryInfoOptions() { Name = mongoClientSettings.LibraryInfo.Name, Version = mongoClientSettings.LibraryInfo.Version },
            };

           return builder;
        }
    }

    internal static class IMongoClientOptionsExtensions
    {

        public static ClusterKey ToClusterKey(this IMongoClientOptions options) => null;
        //{
        //    return new ClusterKey(
        //        options.AllowInsecureTls,
        //        options.ApplicationName,
        //        options.clusterConfigurator,
        //        options.Compressors,
        //        options.connectionMode,
        //        _connectionModeSwitch,
        //        _connectTimeout,
        //        _credentials.ToList(),
        //        _autoEncryptionOptions?.ToCryptClientSettings(),
        //        _directConnection,
        //        _heartbeatInterval,
        //        _heartbeatTimeout,
        //        _ipv6,
        //        options.Clu
        //        _libraryInfo,
        //        _loadBalanced,
        //        _localThreshold,
        //        _loggingSettings,
        //        _maxConnecting,
        //        _maxConnectionIdleTime,
        //        _maxConnectionLifeTime,
        //        _maxConnectionPoolSize,
        //        _minConnectionPoolSize,
        //        MongoDefaults.TcpReceiveBufferSize, // TODO: add ReceiveBufferSize to MongoClientSettings?
        //        _replicaSetName,
        //        _scheme,
        //        _sdamLogFilename,
        //        MongoDefaults.TcpSendBufferSize, // TODO: add SendBufferSize to MongoClientSettings?
        //        _serverApi,
        //        _servers.ToList(),
        //        _serverMonitoringMode,
        //        _serverSelectionTimeout,
        //        _socketTimeout,
        //        _srvMaxHosts,
        //        _srvServiceName,
        //        _sslSettings,
        //        _useTls,
        //        _waitQueueSize,
        //        _waitQueueTimeout);

        //    return new ClusterKey(
        //        _allowInsecureTls,
        //        _applicationName,
        //        _clusterConfigurator,
        //        _compressors,
        //        _connectionMode,
        //        _connectionModeSwitch,
        //        _connectTimeout,
        //        _credentials.ToList(),
        //        _autoEncryptionOptions?.ToCryptClientSettings(),
        //        _directConnection,
        //        _heartbeatInterval,
        //        _heartbeatTimeout,
        //        _ipv6,
        //        _libraryInfo,
        //        _loadBalanced,
        //        _localThreshold,
        //        _loggingSettings,
        //        _maxConnecting,
        //        _maxConnectionIdleTime,
        //        _maxConnectionLifeTime,
        //        _maxConnectionPoolSize,
        //        _minConnectionPoolSize,
        //        MongoDefaults.TcpReceiveBufferSize, // TODO: add ReceiveBufferSize to MongoClientSettings?
        //        _replicaSetName,
        //        _scheme,
        //        _sdamLogFilename,
        //        MongoDefaults.TcpSendBufferSize, // TODO: add SendBufferSize to MongoClientSettings?
        //        _serverApi,
        //        _servers.ToList(),
        //        _serverMonitoringMode,
        //        _serverSelectionTimeout,
        //        _socketTimeout,
        //        _srvMaxHosts,
        //        _srvServiceName,
        //        _sslSettings,
        //        _useTls,
        //        _waitQueueSize,
        //        _waitQueueTimeout);
        //}s
    }
}
