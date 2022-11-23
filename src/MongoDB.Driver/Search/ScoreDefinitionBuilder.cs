﻿// Copyright 2010-present MongoDB Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver.Core.Misc;

namespace MongoDB.Driver
{
    /// <summary>
    /// A builder for a score modifier.
    /// </summary>
    /// <typeparam name="TDocument">The type of the document.</typeparam>
    public sealed class ScoreDefinitionBuilder<TDocument>
    {
        /// <summary>
        /// Creates a score modifier that multiplies a result's base score by a given number.
        /// </summary>
        /// <param name="value">The number to multiply the default base score by.</param>
        /// <returns>
        /// A boost score modifier.
        /// </returns>
        public ScoreDefinition<TDocument> Boost(double value) => new BoostValueScoreDefinition<TDocument>(value);

        /// <summary>
        /// Creates a score modifier that multiples a result's base score by the value of a numeric
        /// field in the documents.
        /// </summary>
        /// <param name="path">
        /// The path to the numeric field whose value to multiply the default base score by.
        /// </param>
        /// <param name="undefined">
        /// The numeric value to substitute if the numeric field is not found in the documents.
        /// </param>
        /// <returns>
        /// A boost score modifier.
        /// </returns>
        public ScoreDefinition<TDocument> Boost(PathDefinition<TDocument> path, double undefined = 0) =>
            new BoostPathScoreDefinition<TDocument>(path, undefined);

        /// <summary>
        /// Creates a score modifier that multiplies a result's base score by the value of a numeric
        /// field in the documents.
        /// </summary>
        /// <param name="path">
        /// The path to the numeric field whose value to multiply the default base score by.
        /// </param>
        /// <param name="undefined">
        /// The numeric value to substitute if the numeric field is not found in the documents.
        /// </param>
        /// <returns>
        /// A boost score modifier.
        /// </returns>
        public ScoreDefinition<TDocument> Boost(Expression<Func<TDocument, double>> path, double undefined = 0) =>
            Boost(new ExpressionFieldDefinition<TDocument>(path), undefined);

        /// <summary>
        /// Creates a score modifier that replaces the base score with a given number.
        /// </summary>
        /// <param name="value">The number to replace the base score with.</param>
        /// <returns>
        /// A constant score modifier.
        /// </returns>
        public ScoreDefinition<TDocument> Constant(double value) =>
            new ConstantScoreDefinition<TDocument>(value);
    }

    internal class BoostValueScoreDefinition<TDocument> : ScoreDefinition<TDocument>
    {
        private readonly double _value;

        public BoostValueScoreDefinition(double value)
        {
            _value = Ensure.IsGreaterThanZero(value, nameof(value));
        }

        public override BsonDocument Render(IBsonSerializer<TDocument> documentSerializer, IBsonSerializerRegistry serializerRegistry) => new()
        {
            { "boost",  new BsonDocument("value", _value) }
        };
    }

    internal class BoostPathScoreDefinition<TDocument> : ScoreDefinition<TDocument>
    {
        private readonly PathDefinition<TDocument> _path;
        private readonly double _undefined;

        public BoostPathScoreDefinition(PathDefinition<TDocument> path, double undefined)
        {
            _path = Ensure.IsNotNull(path, nameof(path));
            _undefined = undefined;
        }

        public override BsonDocument Render(IBsonSerializer<TDocument> documentSerializer, IBsonSerializerRegistry serializerRegistry)
        {
            var document = new BsonDocument()
            {
                { "path", _path.Render(documentSerializer, serializerRegistry) },
                { "undefined", _undefined, _undefined != 0 }
            };

            return new("boost", document);
        }
    }
    
    internal sealed class ConstantScoreDefinition<TDocument> : ScoreDefinition<TDocument>
    {
        private readonly double _value;

        public ConstantScoreDefinition(double value)
        {
            _value = Ensure.IsGreaterThanZero(value, nameof(value));
        }

        public override BsonDocument Render(IBsonSerializer<TDocument> documentSerializer, IBsonSerializerRegistry serializerRegistry) => new()
        {
            { "constant", new BsonDocument("value", _value) }
        };
    }
}
