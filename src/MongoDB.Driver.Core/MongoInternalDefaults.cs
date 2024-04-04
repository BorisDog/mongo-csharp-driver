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
using System.Threading;
using MongoDB.Driver.Core.Servers;

namespace MongoDB.Driver
{
    internal static class MongoInternalDefaults
    {
        public static class Logging
        {
            public const int MaxDocumentSize = 1000;
        }

        public static class  ServerSettings
        {
            public static TimeSpan ConnectTimeout { get; } = TimeSpan.FromSeconds(30);
            public static TimeSpan LocalThreshold { get; } = TimeSpan.FromMilliseconds(15);
            public static ServerMonitoringMode ServerMonitoringMode { get; } = ServerMonitoringMode.Auto;
            public static TimeSpan ServerSelectionTimeout { get; } = TimeSpan.FromSeconds(30);
            public static TimeSpan SocketTimeout { get; } = TimeSpan.Zero;
            public static int SrvMaxHosts { get; } = 0;
            public static string SrvServiceName { get; } = "mongodb";
            public static bool UserTls { get; } = false;
        }

        public static class ConnectionPool
        {
            public const int MaxConnecting = 2;
            public const int MaxConnectionPoolSize = 100;
            public const int MinConnectionPoolSize = 0;

            public static TimeSpan HeartbeatInterval { get; } = TimeSpan.FromSeconds(10);
            public static TimeSpan HeartbeatTimeout { get; } = Timeout.InfiniteTimeSpan;
            public static TimeSpan MaxConnectionIdleTime { get; } = TimeSpan.FromMinutes(10);
            public static TimeSpan MaxConnectionLifeTime { get; } = TimeSpan.FromMinutes(30);
            public static TimeSpan WaitQueueTime { get; } = TimeSpan.FromMinutes(2);
        }
    }
}
