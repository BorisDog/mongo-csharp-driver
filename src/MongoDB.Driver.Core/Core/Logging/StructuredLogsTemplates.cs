﻿/* Copyright 2010-present MongoDB Inc.
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
using Microsoft.Extensions.Logging;
using MongoDB.Driver.Core.Clusters;
using MongoDB.Driver.Core.Connections;
using MongoDB.Driver.Core.Events;
using MongoDB.Driver.Core.Misc;
using MongoDB.Driver.Core.Servers;

namespace MongoDB.Driver.Core.Logging
{
    internal static partial class StructuredLogsTemplates
    {
        public const string ClusterId = nameof(ClusterId);
        public const string Command = nameof(Command);
        public const string CommandName = nameof(CommandName);
        public const string DatabaseName = nameof(DatabaseName);
        public const string Description = nameof(Description);
        public const string DriverConnectionId = nameof(DriverConnectionId);
        public const string DurationMS = nameof(DurationMS);
        public const string Failure = nameof(Failure);
        public const string MaxConnecting = nameof(MaxConnecting);
        public const string MaxIdleTimeMS = nameof(MaxIdleTimeMS);
        public const string MaxPoolSize = nameof(MaxPoolSize);
        public const string Message = nameof(Message);
        public const string MinPoolSize = nameof(MinPoolSize);
        public const string OperationId = nameof(OperationId);
        public const string RequestId = nameof(RequestId);
        public const string Reply = nameof(Reply);
        public const string Reason = nameof(Reason);
        public const string ServerHost = nameof(ServerHost);
        public const string ServerPort = nameof(ServerPort);
        public const string ServerConnectionId = nameof(ServerConnectionId);
        public const string ServiceId = nameof(ServiceId);
        public const string SharedLibraryVersion = nameof(SharedLibraryVersion);
        public const string WaitQueueTimeoutMS = nameof(WaitQueueTimeoutMS);
        public const string WaitQueueSize = nameof(WaitQueueSize);

        public const string ClusterId_Message = $"{{{ClusterId}}} {{{Message}}}";
        public const string DriverConnectionId_Message = $"{{{DriverConnectionId}}} {{{Message}}}";
        public const string ServerId_Message = $"{{{ClusterId}}} {{{ServerHost}}} {{{ServerPort}}} {{{Message}}}";
        public const string ServerId_Message_Description = $"{{{ClusterId}}} {{{ServerHost}}} {{{ServerPort}}} {{{Message}}} {{{Description}}}";
        public const string ClusterId_Message_SharedLibraryVersion = $"{{{ClusterId}}} {{{Message}}} {{{SharedLibraryVersion}}}";

        private readonly static LogsTemplateProvider[] __eventsTemplates;

        static StructuredLogsTemplates()
        {
            var eventTypesCount = Enum.GetValues(typeof(EventType)).Length;
            __eventsTemplates = new LogsTemplateProvider[eventTypesCount];

            AddClusterTemplates();
            AddCmapTemplates();
            AddCommandTemplates();
            AddConnectionTemplates();
            AddSdamTemplates();
        }

        public static LogsTemplateProvider GetTemplateProvider(EventType eventType) => __eventsTemplates[(int)eventType];

        public static object[] GetParams(ClusterId clusterId, object arg1)
        {
            return new object[] { clusterId.Value, arg1 };
        }

        public static object[] GetParams(ClusterId clusterId, object arg1, object arg2)
        {
            return new object[] { clusterId.Value, arg1, arg2 };
        }

        public static object[] GetParams(ClusterId clusterId, EndPoint endPoint, object arg1)
        {
            var (host, port) = endPoint.GetHostAndPort();

            return new object[] { clusterId.Value, host, port, arg1 };
        }

        public static object[] GetParams(ServerId serverId, object arg1)
        {
            var (host, port) = serverId.EndPoint.GetHostAndPort();

            return new object[] { serverId.ClusterId.Value, host, port, arg1 };
        }

        public static object[] GetParams(ServerId serverId, object arg1, object arg2)
        {
            var (host, port) = serverId.EndPoint.GetHostAndPort();

            return new object[] { serverId.ClusterId.Value, host, port, arg1, arg2 };
        }

        public static object[] GetParams(ServerId serverId, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6)
        {
            var (host, port) = serverId.EndPoint.GetHostAndPort();

            return new object[] { serverId.ClusterId.Value, host, port, arg1, arg2, arg3, arg4, arg5, arg6 };
        }

        public static object[] GetParams(ConnectionId connectionId, object arg1)
        {
            var (host, port) = connectionId.ServerId.EndPoint.GetHostAndPort();

            return new object[] { connectionId.ServerId.ClusterId.Value, connectionId.LocalValue, host, port, arg1};
        }

        public static object[] GetParams(ConnectionId connectionId, object arg1, object arg2)
        {
            var (host, port) = connectionId.ServerId.EndPoint.GetHostAndPort();

            return new object[] { connectionId.ServerId.ClusterId.Value, connectionId.LocalValue, host, port, arg1, arg2 };
        }

        public static object[] GetParamsOmitNull(ConnectionId connectionId, object arg1, object arg2, object arg3, object arg4, object arg5, object arg6, object arg7, object ommitableParam)
        {
            var (host, port) = connectionId.ServerId.EndPoint.GetHostAndPort();

            if (ommitableParam == null)
                return new object[] { connectionId.ServerId.ClusterId.Value, connectionId.LocalValue, host, port, arg1, arg2, arg3, arg4, arg5, arg6, arg7, };
            else
                return new object[] { connectionId.ServerId.ClusterId.Value, connectionId.LocalValue, host, port, arg1, arg2, arg3, arg4, arg5, arg6, arg7, ommitableParam };
        }

        private static void AddTemplateProvider<TEvent>(LogLevel logLevel, string template, Func<TEvent, object[]> extractor) where TEvent : struct, IEvent =>
            AddTemplateProvider<TEvent>(new LogsTemplateProvider(
                logLevel,
                new[] { template },
                extractor));

        private static void AddTemplateProvider<TEvent>(LogLevel logLevel, string[] templates, Func<TEvent, object[]> extractor, Func<TEvent, LogsTemplateProvider, string> templateExtractor) where TEvent : struct, IEvent =>
            AddTemplateProvider<TEvent>(new LogsTemplateProvider(
                logLevel,
                templates,
                extractor,
                templateExtractor));

        private static void AddTemplate<TEvent, TArg>(LogLevel logLevel, string template, Func<TEvent, TArg, object[]> extractor) where TEvent : struct, IEvent =>
            AddTemplateProvider<TEvent>(new LogsTemplateProvider(
                logLevel,
                new[] { template },
                extractor));

        private static void AddTemplateProvider<TEvent>(LogsTemplateProvider templateProvider) where TEvent : struct, IEvent
        {
            var index = (int)(new TEvent().Type);

            if (__eventsTemplates[index] != null)
            {
                throw new InvalidOperationException($"Template already registered for {typeof(TEvent)} event");
            }

            __eventsTemplates[index] = templateProvider;
        }

        private static string Concat(params string[] parameters) =>
            string.Join(" ", parameters.Select(p => $"{{{p}}}"));

        private static string Concat(string[] parameters, params string[] additionalParameters) =>
            string.Join(" ", parameters.Concat(additionalParameters).Select(p => $"{{{p}}}"));

        internal sealed class LogsTemplateProvider
        {
            public LogLevel LogLevel { get; }
            public string[] Templates { get; }
            public Delegate ParametersExtractor { get; }
            public Delegate TemplateExtractor { get; }

            public LogsTemplateProvider(LogLevel logLevel, string[] templates, Delegate parametersExtractor, Delegate templateExtractor = null)
            {
                LogLevel = logLevel;
                Templates = templates;
                ParametersExtractor = parametersExtractor;
                TemplateExtractor = templateExtractor;
            }

            public string GetTemplate<TEvent>(TEvent @event) where TEvent : struct, IEvent =>
                TemplateExtractor != null ? ((Func<TEvent, LogsTemplateProvider, string>)TemplateExtractor)(@event, this) : Templates.First();

            public object[] GetParams<TEvent>(TEvent @event) where TEvent : struct, IEvent =>
                (ParametersExtractor as Func<TEvent, object[]>)(@event);

            public object[] GetParams<TEvent, TArg>(TEvent @event, TArg arg) where TEvent : struct, IEvent =>
                (ParametersExtractor as Func<TEvent, TArg, object[]>)(@event, arg);
        }
    }
}
