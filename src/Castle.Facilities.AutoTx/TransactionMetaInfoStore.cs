#region License
// Copyright 2004-2024 Castle Project - https://www.castleproject.org/
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
using Castle.Services.Transaction.Utilities;

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

        private readonly Dictionary<Type, TransactionMetaInfo> _implementationTypeToMetaInfo = [];

        /// <summary>
        /// Creates <see cref="TransactionMetaInfo" /> from a type.
        /// </summary>
        public TransactionMetaInfo CreateMetaInfoFrom(Type implementationType)
        {
            var metaInfo = new TransactionMetaInfo();

            PopulateMetaInfoFrom(metaInfo, implementationType);

            RegisterMetaInfo(implementationType, metaInfo);

            return metaInfo;
        }

        private static void PopulateMetaInfoFrom(TransactionMetaInfo metaInfo, Type? implementationType)
        {
            if (implementationType is null ||
                implementationType == typeof(object) ||
                implementationType == typeof(MarshalByRefObject))
            {
                return;
            }

            var methods = implementationType.GetMethods(BindingFlags);

            foreach (var method in methods)
            {
                var attributes = method.GetCustomAttributes(typeof(TransactionAttribute), true);

                if (attributes.Length > 0)
                {
                    metaInfo.AddTransactionalMethod(method, (TransactionAttribute) attributes[0]);

                    // Only add the method as transaction injection if we also have specified a transaction attribute.
                    attributes = method.GetCustomAttributes(typeof(InjectTransactionAttribute), true);

                    if (attributes.Length > 0)
                    {
                        metaInfo.AddTransactionInjectionTo(method);
                    }
                }
            }

            PopulateMetaInfoFrom(metaInfo, implementationType.BaseType);
        }

        /// <summary>
        /// Create <see cref="TransactionMetaInfo" /> from the configuration
        /// that specify what methods should be overridden.
        /// </summary>
        public TransactionMetaInfo CreateMetaInfoFromConfiguration(Type implementationType, IList<MethodInfo> methods, IConfiguration configuration)
        {
            var metaInfo = GetMetaInfoFor(implementationType) ??
                           new TransactionMetaInfo();

            foreach (var method in methods)
            {
                var transactionModeName = configuration.Attributes[TransactionModeAttribute];
                var isolationLevelName = configuration.Attributes[IsolationLevelAttribute];

                var transactionMode = ParseTransactionModeName(implementationType, method, transactionModeName);
                var isolationLevel = ParseIsolationLevelName(implementationType, method, isolationLevelName);

                metaInfo.AddTransactionalMethod(method, new TransactionAttribute(transactionMode, isolationLevel));
            }

            RegisterMetaInfo(implementationType, metaInfo);

            return metaInfo;
        }

        /// <summary>
        /// Gets the <see cref="TransactionMetaInfo" /> for the implementation type.
        /// </summary>
        /// <param name="implementationType"></param>
        /// <returns></returns>
        public TransactionMetaInfo? GetMetaInfoFor(Type implementationType)
        {
            _implementationTypeToMetaInfo.TryGetValue(implementationType, out var metaInfo);

            return metaInfo;
        }

        private void RegisterMetaInfo(Type implementationType, TransactionMetaInfo metaInfo)
        {
            _implementationTypeToMetaInfo[implementationType] = metaInfo;
        }

        private static TransactionMode ParseTransactionModeName(Type implementationType, MethodInfo method, string? transactionModeName)
        {
            if (transactionModeName.IsNullOrEmpty())
            {
                return TransactionMode.Unspecified;
            }

            if (!Enum.TryParse(transactionModeName, true, out TransactionMode transactionMode))
            {
                var values = (string[])
#if NET5_0_OR_GREATER
                    Enum.GetValues<TransactionMode>().Select(static x => x.ToString());
#else
                    Enum.GetValues(typeof(TransactionMode));
#endif

                throw new FacilityException(
                    $"The configuration for the class '{implementationType.FullName}', method '{method.Name}', " +
                    $"has specified '{transactionModeName}' on '{TransactionModeAttribute}' attribute which is not supported. " +
                    $"The possible values are '{string.Join(", ", values)}'.");
            }

            return transactionMode;
        }

        private static IsolationLevel ParseIsolationLevelName(Type implementationType, MethodInfo method, string? isolationLevelName)
        {
            if (isolationLevelName.IsNullOrEmpty())
            {
                return IsolationLevel.Unspecified;
            }

            if (!Enum.TryParse(isolationLevelName, true, out IsolationLevel isolationLevel))
            {
                var values = (string[])
#if NET5_0_OR_GREATER
                    Enum.GetValues<IsolationLevel>().Select(static x => x.ToString());
#else
                    Enum.GetValues(typeof(IsolationLevel));
#endif

                throw new FacilityException(
                    $"The configuration for the class '{implementationType.FullName}', method '{method.Name}', " +
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
            return null!;
        }
    }
}
