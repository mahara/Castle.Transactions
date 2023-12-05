#region License
// Copyright 2004-2023 Castle Project - https://www.castleproject.org/
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

using System.Reflection;
using System.Transactions;

using Castle.Core.Configuration;

using Castle.MicroKernel.Facilities;

using Castle.Services.Transaction;

namespace Castle.Facilities.AutoTx
{
    /// <summary>
    /// A store for <see cref="TransactionMetaInfo" />.
    /// </summary>
    public class TransactionMetaInfoStore : MarshalByRefObject
    {
        private const string TransactionModeAttribute = "transactionMode";
        private const string IsolationLevelAttribute = "isolationLevel";

        private static readonly BindingFlags BindingFlags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly;

        private readonly Dictionary<Type, TransactionMetaInfo> _typeToMetaInfo = new();

        /// <summary>
        /// Creates <see cref="TransactionMetaInfo" /> from a type.
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
            if (implementation is null ||
                implementation == typeof(object) ||
                implementation == typeof(MarshalByRefObject))
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
        /// Create <see cref="TransactionMetaInfo" /> from the configuration
        /// that specify what methods should be overridden.
        /// </summary>
        public TransactionMetaInfo CreateMetaInfoFromConfiguration(Type implementation, IList<MethodInfo> methods, IConfiguration configuration)
        {
            var metaInfo = GetMetaInfoFor(implementation) ?? new TransactionMetaInfo();

            foreach (var method in methods)
            {
                var transactionModeName = configuration.Attributes[TransactionModeAttribute];
                var isolationLevelName = configuration.Attributes[IsolationLevelAttribute];

                var transactionMode = ParseTransactionModeName(implementation, method, transactionModeName);
                var isolationLevel = ParseIsolationLevelName(implementation, method, isolationLevelName);

                metaInfo.Add(method, new TransactionAttribute(transactionMode, isolationLevel));
            }

            RegisterMetaInfo(implementation, metaInfo);

            return metaInfo;
        }

        /// <summary>
        /// Gets the <see cref="TransactionMetaInfo" /> for the implementation.
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

        private static TransactionMode ParseTransactionModeName(Type implementation, MethodInfo method, string transactionModeName)
        {
            if (string.IsNullOrEmpty(transactionModeName))
            {
                return TransactionMode.Unspecified;
            }

            if (!Enum.TryParse(transactionModeName, true, out TransactionMode transactionMode))
            {
                var values = (string[]) Enum.GetValues(typeof(TransactionMode));

                throw new FacilityException(
                    $"The configuration for the class '{implementation.FullName}', method '{method.Name}', " +
                    $"has specified '{transactionModeName}' on '{TransactionModeAttribute}' attribute which is not supported. " +
                    $"The possible values are '{string.Join(", ", values)}'.");
            }

            return transactionMode;
        }

        private static IsolationLevel ParseIsolationLevelName(Type implementation, MethodInfo method, string isolationLevelName)
        {
            if (string.IsNullOrEmpty(isolationLevelName))
            {
                return IsolationLevel.Unspecified;
            }

            if (!Enum.TryParse(isolationLevelName, true, out IsolationLevel isolationLevel))
            {
                var values = (string[]) Enum.GetValues(typeof(IsolationLevel));

                throw new FacilityException(
                    $"The configuration for the class '{implementation.FullName}', method '{method.Name}', " +
                    $"has specified '{isolationLevelName}' on '{IsolationLevelAttribute}' attribute which is not supported. " +
                    $"The possible values are '{string.Join(", ", values)}'.");
            }

            return isolationLevel;
        }

#if NET
        //[Obsolete("Obsoletions.RemotingApisMessage, DiagnosticId = Obsoletions.RemotingApisDiagId, UrlFormat = Obsoletions.SharedUrlFormat")]
        [Obsolete("This Remoting API is not supported and throws PlatformNotSupportedException.", DiagnosticId = "SYSLIB0010", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
