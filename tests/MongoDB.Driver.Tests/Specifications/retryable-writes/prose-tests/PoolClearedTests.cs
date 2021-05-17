/* Copyright 2021-present MongoDB Inc.
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
using System.Linq;
using System.Net;
using FluentAssertions;
using MongoDB.Bson;
using MongoDB.Bson.TestHelpers;
using MongoDB.Bson.TestHelpers.XunitExtensions;
using MongoDB.Driver.Core;
using MongoDB.Driver.Core.Bindings;
using MongoDB.Driver.Core.Clusters.ServerSelectors;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.TestHelpers;
using MongoDB.Driver.Core.TestHelpers.XunitExtensions;
using Xunit;

namespace MongoDB.Driver.Tests.Specifications.retryable_writes.prose_tests
{
    public class PoolClearedTests
    {
        [SkippableTheory]
        [ParameterAttributeData]
        public void Should_retry_after_PoolClearedError_on_write([Values(false, true)] bool async)
        {
            RequireServer.Check().Supports(Feature.RetryableWrites, Feature.FailPointsBlockConnection);

            var failPointCommand = BsonDocument.Parse(
            $@"{{
                    configureFailPoint : 'failCommand',
                    mode : {{ 'times' : 1 }},
                    data :
                    {{
                        failCommands : [ 'insert' ],
                        errorCode : 91,
                        blockConnection: true,
                        blockTimeMS: 1000
                    }}
                }}");

            var eventCapturer = new EventCapturer()
              .Capture<CommandStartedEvent>()
              .Capture<CommandSucceededEvent>()
              .Capture<ConnectionPoolClearedEvent>()
              .Capture<ConnectionPoolCheckedOutConnectionEvent>()
              .Capture<ConnectionPoolCheckingOutConnectionFailedEvent>();

            var settings = GetMongoClientSettings(eventCapturer);
            var serverAddress = settings.Server;
            var failpointServer = DriverTestConfiguration.Client.Cluster.SelectServer(new EndPointServerSelector(new DnsEndPoint(serverAddress.Host, serverAddress.Port)), default);

            using var failPoint = FailPoint.Configure(failpointServer, NoCoreSession.NewHandle(), failPointCommand);
            using var client = DriverTestConfiguration.CreateDisposableClient(settings);

            var database = client.GetDatabase(DriverTestConfiguration.DatabaseNamespace.DatabaseName);
            var collection = database.GetCollection<BsonDocument>(DriverTestConfiguration.CollectionNamespace.CollectionName);
            database.DropCollection(collection.CollectionNamespace.CollectionName);

            ThreadingUtilities.ExecuteOnNewThreads(2, i =>
            {
                var document = new BsonDocument("_id", i);

                if (async)
                {
                    collection.InsertOneAsync(document).GetAwaiter().GetResult();
                }
                else
                {
                    collection.InsertOne(document);
                }
            }, 20000);

            eventCapturer.Events
                .OfType<CommandStartedEvent>()
                .Where(c => c.CommandName == "insert")
                .Count()
                .Should().Be(3);

            var finished = eventCapturer.Events
               .OfType<CommandSucceededEvent>().ToArray();

            eventCapturer.Events
                .OfType<ConnectionPoolCheckingOutConnectionFailedEvent>()
                .Count()
                .Should().Be(1);

            eventCapturer.Events
                .OfType<ConnectionPoolClearedEvent>()
                .Count()
                .Should().Be(1);
        }

        // private methods
        private MongoClientSettings GetMongoClientSettings(EventCapturer eventCapturer)
        {
            var clonedClientSettings = DriverTestConfiguration.Client.Settings.Clone();
            clonedClientSettings.HeartbeatInterval = TimeSpan.FromMilliseconds(100);
            clonedClientSettings.MaxConnectionPoolSize = 1;
            clonedClientSettings.RetryWrites = true;
            clonedClientSettings.ClusterConfigurator = builder => builder.Subscribe(eventCapturer);
            clonedClientSettings.Servers = new[] { clonedClientSettings.Servers.First() };

            return clonedClientSettings;
        }
    }
}
