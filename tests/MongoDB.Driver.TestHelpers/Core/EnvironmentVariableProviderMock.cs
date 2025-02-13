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

using MongoDB.Driver.Core.Misc;
using Moq;

namespace MongoDB.Driver.TestHelpers.Core;

internal static class EnvironmentVariableProviderMock
{
    public static Mock<IEnvironmentVariableProvider> Create(params string[] env)
    {
        var environmentVariableProviderMock = new Mock<IEnvironmentVariableProvider>();
        if (env != null)
        {
            foreach (var variable in env)
            {
                var parts = variable.Split('=');
                var variableName = parts[0];
                var variableValue = parts.Length == 1 ? "1" : parts[1];
                environmentVariableProviderMock
                    .Setup(e => e.GetEnvironmentVariable(variableName))
                    .Returns(variableValue);
            }
        }

        return environmentVariableProviderMock;
    }
}
