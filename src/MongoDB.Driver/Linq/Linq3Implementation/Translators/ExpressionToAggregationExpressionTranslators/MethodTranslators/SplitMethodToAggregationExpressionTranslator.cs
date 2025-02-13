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
using System.Linq.Expressions;
using System.Reflection;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver.Linq.Linq3Implementation.Ast.Expressions;
using MongoDB.Driver.Linq.Linq3Implementation.ExtensionMethods;
using MongoDB.Driver.Linq.Linq3Implementation.Misc;

namespace MongoDB.Driver.Linq.Linq3Implementation.Translators.ExpressionToAggregationExpressionTranslators.MethodTranslators
{
    internal static class SplitMethodToAggregationExpressionTranslator
    {
        private static readonly MethodInfo[] __splitMethods = new[]
        {
            StringMethod.SplitWithChars,
            StringMethod.SplitWithCharsAndCount,
            StringMethod.SplitWithCharsAndCountAndOptions,
            StringMethod.SplitWithCharsAndOptions,
            StringMethod.SplitWithStringsAndCountAndOptions,
            StringMethod.SplitWithStringsAndOptions
        };

        private static readonly MethodInfo[] __splitWithCharsMethods = new[]
        {
            StringMethod.SplitWithChars,
            StringMethod.SplitWithCharsAndCount,
            StringMethod.SplitWithCharsAndCountAndOptions,
            StringMethod.SplitWithCharsAndOptions
        };

        private static readonly MethodInfo[] __splitWithCountMethods = new[]
        {
            StringMethod.SplitWithCharsAndCount,
            StringMethod.SplitWithCharsAndCountAndOptions,
            StringMethod.SplitWithStringsAndCountAndOptions,
        };

        private static readonly MethodInfo[] __splitWithOptionsMethods = new[]
       {
            StringMethod.SplitWithCharsAndCountAndOptions,
            StringMethod.SplitWithCharsAndOptions,
            StringMethod.SplitWithStringsAndCountAndOptions,
            StringMethod.SplitWithStringsAndOptions
        };

        private static readonly MethodInfo[] __splitWithStringsMethods = new[]
        {
            StringMethod.SplitWithStringsAndCountAndOptions,
            StringMethod.SplitWithStringsAndOptions
        };

        public static TranslatedExpression Translate(TranslationContext context, MethodCallExpression expression)
        {
            var method = expression.Method;
            var arguments = expression.Arguments;

            if (method.IsOneOf(__splitMethods))
            {
                var stringExpression = expression.Object;
                var stringTranslation = ExpressionToAggregationExpressionTranslator.Translate(context, stringExpression);

                string delimiter;
                if (method.IsOneOf(__splitWithCharsMethods))
                {
                    var separatorsExpression = arguments[0];
                    var separatorChars = separatorsExpression.GetConstantValue<char[]>(containingExpression: expression);
                    if (separatorChars.Length != 1)
                    {
                        goto notSupported;
                    }
                    delimiter = new string(separatorChars[0], 1);
                }
                else if (method.IsOneOf(__splitWithStringsMethods))
                {
                    var separatorsExpression = arguments[0];
                    var separatorStrings = separatorsExpression.GetConstantValue<string[]>(containingExpression: expression);
                    if (separatorStrings.Length != 1)
                    {
                        goto notSupported;
                    }
                    delimiter = separatorStrings[0];
                }
                else
                {
                    goto notSupported;
                }

                var ast = AstExpression.Split(stringTranslation.Ast, delimiter);

                var options = StringSplitOptions.None;
                if (method.IsOneOf(__splitWithOptionsMethods))
                {
                    var optionsExpression = arguments.Last();
                    options = optionsExpression.GetConstantValue<StringSplitOptions>(containingExpression: expression);
                }
                if (options == StringSplitOptions.RemoveEmptyEntries)
                {
                    ast = AstExpression.Filter(
                        input: ast,
                        cond: AstExpression.Ne(AstExpression.Var("item"), ""),
                        @as: "item");
                }

                if (method.IsOneOf(__splitWithCountMethods))
                {
                    var countExpression = arguments[1];
                    var countTranslation = ExpressionToAggregationExpressionTranslator.Translate(context, countExpression);
                    ast = AstExpression.Slice(ast, countTranslation.Ast);
                }

                var serializer = new ArraySerializer<string>(new StringSerializer());
                return new TranslatedExpression(expression, ast, serializer);
            }

        notSupported:
            throw new ExpressionNotSupportedException(expression);
        }
    }
}
