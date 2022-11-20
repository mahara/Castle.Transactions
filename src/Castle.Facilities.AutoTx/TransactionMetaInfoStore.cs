#region License
// Copyright 2004-2022 Castle Project - https://www.castleproject.org/
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

namespace Castle.Facilities.AutoTx
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Transactions;

    using Core.Configuration;

    using MicroKernel.Facilities;

    using Services.Transaction;

    /// <summary>
    /// A store for <see cref="TransactionMetaInfo" />.
    /// </summary>
    public class TransactionMetaInfoStore : MarshalByRefObject
    {
        private static readonly BindingFlags BindingFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;
        private static readonly string TransactionModeAttribute = "transactionMode";
        private static readonly string IsolationLevelAttribute = "isolationLevel";

        private readonly Dictionary<Type, TransactionMetaInfo> _typeToMetaInfo = new();

#if NET
        [Obsolete]
#endif
        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// <summary>
        /// Creates meta-information from a type.
        /// </summary>
        public TransactionMetaInfo CreateMetaInfoFromType(Type implementation)
        {
            var metaInfo = new TransactionMetaInfo();

            PopulateMetaInfoFromType(metaInfo, implementation);

            RegisterMetaInfo(implementation, metaInfo);

            return metaInfo;
        }

        private static void PopulateMetaInfoFromType(TransactionMetaInfo metaInfo, Type implementation)
        {
            if (implementation == typeof(object) || implementation == typeof(MarshalByRefObject))
            {
                return;
            }

            var methods = implementation.GetMethods(BindingFlags);

            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(TransactionAttribute), true);
                if (attributes.Length > 0)
                {
                    metaInfo.Add(method, (TransactionAttribute) attributes[0]);

                    // Only add the method as transaction injection if we also have specified a transaction attribute.
                    attributes = method.GetCustomAttributes(typeof(InjectTransactionAttribute), true);
                    if (attributes.Length > 0)
                    {
                        metaInfo.AddInjection(method);
                    }
                }
            }

            PopulateMetaInfoFromType(metaInfo, implementation.BaseType);
        }

        /// <summary>
        /// Create meta-information from the configuration about what methods should be overridden.
        /// </summary>
        public TransactionMetaInfo CreateMetaInfoFromConfig(Type implementation, IList<MethodInfo> methods, IConfiguration facilityConfiguration)
        {
            var metaInfo = GetMetaInfoFor(implementation);

            metaInfo ??= new TransactionMetaInfo();

            foreach (var method in methods)
            {
                var transactionMode = facilityConfiguration.Attributes[TransactionModeAttribute];
                var isolationLevel = facilityConfiguration.Attributes[IsolationLevelAttribute];

                var mode = GetTransactionMode(implementation, method, transactionMode);
                var level = GetIsolationLevel(implementation, method, isolationLevel);

                metaInfo.Add(method, new TransactionAttribute(mode, level));
            }

            RegisterMetaInfo(implementation, metaInfo);

            return metaInfo;
        }

        /// <summary>
        /// Gets the meta-information for the implementation.
        /// </summary>
        /// <param name="implementation"></param>
        /// <returns></returns>
        public TransactionMetaInfo GetMetaInfoFor(Type implementation)
        {
            _typeToMetaInfo.TryGetValue(implementation, out var metaInfo);

            return metaInfo;
        }

        private void RegisterMetaInfo(Type implementation, TransactionMetaInfo metaInfo)
        {
            _typeToMetaInfo[implementation] = metaInfo;
        }

        private static TransactionScopeOption GetTransactionMode(Type implementation, MethodInfo method, string mode)
        {
            if (mode == null)
            {
                return TransactionScopeOption.Required;
            }

            try
            {
                return (TransactionScopeOption) Enum.Parse(typeof(TransactionScopeOption), mode, true);
            }
            catch (Exception)
            {
                var values = (string[]) Enum.GetValues(typeof(TransactionScopeOption));

                var message = string.Format("The configuration for the class {0}, " +
                                            "method {1}, has specified {2} on {3} attribute which is not supported. " +
                                            "The possible values are {4}.",
                                            implementation.FullName,
                                            method.Name,
                                            mode,
                                            TransactionModeAttribute,
                                            string.Join(", ", values));
                throw new FacilityException(message);
            }
        }

        private static IsolationLevel GetIsolationLevel(Type implementation, MethodInfo method, string level)
        {
            if (level == null)
            {
                return IsolationLevel.Unspecified;
            }

            try
            {
                return (IsolationLevel) Enum.Parse(typeof(IsolationLevel), level, true);
            }
            catch (Exception)
            {
                var values = (string[]) Enum.GetValues(typeof(TransactionScopeOption));

                var message = string.Format("The configuration for the class {0}, " +
                                            "method {1}, has specified {2} on {3} attribute which is not supported. " +
                                            "The possible values are {4}.",
                                            implementation.FullName,
                                            method.Name,
                                            level,
                                            IsolationLevelAttribute,
                                            string.Join(", ", values));
                throw new FacilityException(message);
            }
        }
    }
}
