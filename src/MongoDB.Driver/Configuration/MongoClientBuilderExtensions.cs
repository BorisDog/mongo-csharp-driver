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
using System.Linq;
using MongoDB.Driver.Core.Events;

namespace MongoDB.Driver.Builders
{
    /// <summary>
    /// <see cref="MongoClientBuilder"/> extensions.
    /// </summary>
    public static class MongoClientBuilderExtensions
    {
        /// <summary>
        /// Updates <paramref name="builder"/> instance with parameters to values specified by <paramref name="connectionString" />.
        /// </summary>
        /// <param name="builder">The source builder.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <returns>
        /// New <see cref="MongoClientBuilder" /> instance.
        /// </returns>
        public static MongoClientBuilder WithConnectionString(this MongoClientBuilder builder, string connectionString) =>
            builder.WithMongoUrl(new MongoUrl(connectionString));

        /// <summary>
        /// Updates <paramref name="builder"/> instance with parameters to values specified by <paramref name="mongoUrl" />.
        /// </summary>
        /// <param name="builder">The source builder.</param>
        /// <param name="mongoUrl">The mongo url.</param>
        /// <returns>
        /// New <see cref="MongoClientBuilder" /> instance.
        /// </returns>
        public static MongoClientBuilder WithMongoUrl(this MongoClientBuilder builder, MongoUrl mongoUrl)
        {
            var result = builder with
            {
                ApplicationName = mongoUrl.ApplicationName,
                AllowInsecureTls = mongoUrl.AllowInsecureTls,
                //...
            };

            return result;
        }
    }

    /// <summary>
    /// <see cref="MongoClientBuilder"/> extensions.
    /// </summary>
    public static class MongoClientBuilderEventsExtensions
    {
        /// <summary>
        /// Add an event handler to existing <see cref="MongoClientBuilder.EventSubscribers"/> event subscribers.
        /// </summary>
        /// <param name="builder">The source builder.</param>
        /// <param name="eventHandler">The event handler action.</param>
        /// <returns>
        /// New <see cref="MongoClientBuilder" /> instance.
        /// </returns>
        public static MongoClientBuilder AddEventHandler<TEvent>(this MongoClientBuilder builder, Action<TEvent> eventHandler) =>
            AddEventSubscriber(builder, new SingleEventSubscriber<TEvent>(eventHandler));

        /// <summary>
        /// Add an eventSubscriber to existing <see cref="MongoClientBuilder.EventSubscribers"/> event subscribers.
        /// </summary>
        /// <param name="builder">The source builder.</param>
        /// <param name="eventSubscriber">The event subscriber.</param>
        /// <returns>
        /// New <see cref="MongoClientBuilder" /> instance.
        /// </returns>
        public static MongoClientBuilder AddEventSubscriber(this MongoClientBuilder builder, IEventSubscriber eventSubscriber)
        {
            var subscribers = builder.EventSubscribers?.ToList() ?? new List<IEventSubscriber>();
            subscribers.Add(eventSubscriber);

            return builder with { EventSubscribers = subscribers };
        }
    }
}
