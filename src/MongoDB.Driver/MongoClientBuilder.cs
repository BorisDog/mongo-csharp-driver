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
using System.Linq;
using MongoDB.Driver.Builders;
using MongoDB.Driver.Configuration;
using MongoDB.Driver.Core;
using MongoDB.Driver.Core.Configuration;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Linq;

namespace MongoDB.Driver
{
    /// <summary>
    ///  <see cref="IMongoClient"/> builder.
    /// </summary>
    public sealed record MongoClientBuilder : IMongoClientOptions
    {
        /// <summary>
        /// Gets or sets whether to relax TLS constraints as much as possible.
        /// Setting this variable to true will also set <see cref="SslSettings.CheckCertificateRevocation"/> to false.
        /// </summary>
        public bool AllowInsecureTls { get; init; }

        /// <summary>
        /// Gets the application name.
        /// </summary>
        public string ApplicationName { get; init; }

        //private AutoEncryptionOptions _autoEncryptionOptions;

        /// <summary>
        /// Gets the compressors.
        /// </summary>
        public IReadOnlyList<Compressor> Compressors { get; init; }

        /// <summary>
        /// Gets the connection settings.
        /// </summary>
        public Builders.ConnectionPoolSettings ConnectionPoolSettings { get; init; }

        /// <summary>
        /// Gets the event subscribers.
        /// </summary>
        public IReadOnlyList<IEventSubscriber> EventSubscribers { get; init; }

        /// <summary>
        /// Gets the scheme.
        /// </summary>
        public ConnectionStringScheme Scheme { get; init; }

        /// <summary>
        /// Gets the replica set name.
        /// </summary>
        public string ReplicaSetName { get; init; }

        /// <summary>
        /// Gets the credential.
        /// </summary>
        public MongoCredential Credential { get; init; }

        /// <summary>
        /// Gets a value indicating whether to connect to the deployment in Single topology.
        /// </summary>
        public bool? DirectConnection { get; init; }

        /// <summary>
        /// Gets a value indicating whether to use IPv6.
        /// </summary>
        public bool IPv6 { get; init; }

        /// <summary>
        /// Gets the library info.
        /// </summary>
        public LibraryInfoOptions LibraryInfoOptions { get; init; }

        /// <summary>
        /// Indicates whether load balanced mode is used.
        /// </summary>
        public bool LoadBalanced { get; init; }

        /// <summary>
        /// Gets the logging options.
        /// </summary>
        public LoggingOptions LoggingOptions { get; init; }

        /// <summary>
        /// Gets the read concern.
        /// </summary>
        public ReadConcern ReadConcern { get; init; }

        /// <summary>
        /// Gets the read preference.
        /// </summary>
        public ReadPreference ReadPreference { get; init; }

        /// <summary>
        /// Gets a value indicating whether to retry reads.
        /// </summary>
        public bool RetryReads { get; init; }

        /// <summary>
        /// Gets a value indicating whether to retry writes.
        /// </summary>
        public bool RetryWrites { get; init; }

        /// <summary>
        /// Gets the serialization settings
        /// </summary>
        public SerializationSettings SerializationSettings { get; init; }

        /// <summary>
        /// Gets the server settings.
        /// </summary>
        public Builders.ServerSettings ServerSettings { get; init; }

        /// <summary>
        /// Gets the write concern.
        /// </summary>
        public WriteConcern WriteConcern { get; init; }

        /// <summary>
        /// Creates a new instance of MongoClientBuilder
        /// </summary>
        public MongoClientBuilder()
        {
        }

        /// <summary>
        /// Builds the <see cref="IMongoClient"/>.
        /// </summary>
        /// <returns>An <see cref="IMongoClient"/> instance.</returns>
        public IMongoClient Build()
        {
            var compressors = Compressors.Select(c =>
                {
                    var config = new CompressorConfiguration(c.Type);
                    config.Properties.AddRange(c.Properties);

                    return config;
                })
                .ToArray();

            var mongoClientSettings = new MongoClientSettings()
            {
                AllowInsecureTls = AllowInsecureTls,
                ApplicationName = ApplicationName,
                //AutoEncryptionOptions = null; // must be configured via code
                Compressors = compressors,
                ConnectTimeout = ServerSettings.ConnectTimeout,
                Credential = Credential,
                DirectConnection = DirectConnection,
                HeartbeatInterval = ServerSettings.HeartbeatInterval,
                HeartbeatTimeout = ServerSettings.HeartbeatTimeout,
                IPv6 = IPv6,
                LibraryInfo = LibraryInfo,
                LinqProvider = LinqProvider.V3,
                LoadBalanced = LoadBalanced,
                LocalThreshold = ServerSettings.LocalThreshold,
                MaxConnecting = ConnectionPoolSettings.MaxConnecting,
                MaxConnectionIdleTime = ConnectionPoolSettings.MaxConnectionIdleTime,
                MaxConnectionLifeTime = ConnectionPoolSettings.MaxConnectionLifeTime,
                MaxConnectionPoolSize = ConnectionPoolSettings.MaxConnectionPoolSize,
                MinConnectionPoolSize = ConnectionPoolSettings.MinConnectionPoolSize,
                ReadConcern = ReadConcern,
                ReadEncoding = SerializationSettings.ReadEncoding,
                ReadPreference = ReadPreference,
                ReplicaSetName = ReplicaSetName,
                RetryReads = RetryReads,
                RetryWrites = RetryWrites,
                Scheme = Scheme,
                Servers = ServerSettings.Servers,
                ServerMonitoringMode = ServerSettings.ServerMonitoringMode,
                ServerSelectionTimeout = ServerSettings.ServerSelectionTimeout,
                SocketTimeout = ServerSettings.SocketTimeout,
                SrvMaxHosts = ServerSettings.SrvMaxHosts,
                SrvServiceName = ServerSettings.SrvServiceName,
                SslSettings = ServerSettings.SslSettings,
                UseTls = ServerSettings.UseTls,
                WaitQueueTimeout = ConnectionPoolSettings.WaitQueueTimeout,
                WriteConcern = WriteConcern,
                WriteEncoding = SerializationSettings.WriteEncoding
            };

            if (EventSubscribers != null)
            {
                mongoClientSettings.ClusterConfigurator = builder => builder.Subscribe(EventSubscribers);
            }

            var mongoClient = new MongoClient(mongoClientSettings);

            return mongoClient;
        }
    }
}
