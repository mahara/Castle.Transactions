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

using Castle.Services.Transaction;

namespace Castle.Facilities.AutoTx
{
    /// <summary>
    /// A storage for attributes found on transactional classes.
    /// </summary>
    public class TransactionMetaInfo : MarshalByRefObject
    {
        private readonly object _lock = new();

        private readonly Dictionary<MethodInfo, TransactionAttribute> _transactionalMethods = new();
        private readonly HashSet<MethodInfo> _transactionalInjectionMethods = new();
        private readonly Dictionary<MethodInfo, string> _nonTransactionalMethods = new();

        /// <summary>
        /// A collection of <see cref="MethodInfo" /> which need transaction.
        /// </summary>
        public ICollection<MethodInfo> TransactionalMethods =>
            _transactionalMethods.Keys;

        /// <summary>
        /// Adds a <see cref="MethodInfo" /> and the corresponding <see cref="TransactionAttribute" />.
        /// </summary>
        public void AddTransactionalMethod(MethodInfo method, TransactionAttribute attribute)
        {
            _transactionalMethods[method] = attribute;
        }

        /// <summary>
        /// Determines whether a <see cref="MethodInfo" /> is a transactional.
        /// </summary>
        /// <param name="method"></param>
        /// <returns>
        /// <see langword="true" /> if the <see cref="MethodInfo" /> is transactional;
        /// otherwise, <see langword="false" />.
        /// </returns>
        public bool IsMethodTransactional(MethodInfo method)
        {
            lock (_lock)
            {
                if (_transactionalMethods.ContainsKey(method))
                {
                    return true;
                }

                if (_nonTransactionalMethods.ContainsKey(method))
                {
                    return false;
                }

                if (method.DeclaringType.IsGenericType || method.IsGenericMethod)
                {
                    return MarkMethodAsTransactionalIfApplicable(method);
                }

                return false;
            }
        }

        private bool MarkMethodAsTransactionalIfApplicable(MethodInfo method)
        {
            var attributes = method.GetCustomAttributes(typeof(TransactionAttribute), true);

            if (attributes.Length > 0)
            {
                AddTransactionalMethod(method, (TransactionAttribute) attributes[0]);

                return true;
            }
            else
            {
                _nonTransactionalMethods[method] = string.Empty;
            }

            return false;
        }

        /// <summary>
        /// Returns the <see cref="TransactionAttribute" /> for a given <see cref="MethodInfo" />.
        /// </summary>
        public TransactionAttribute GetTransactionAttributeFor(MethodInfo method)
        {
            return _transactionalMethods[method];
        }

        /// <summary>
        /// Adds <see cref="MethodInfo" /> to the list of <see cref="MethodInfo" />
        /// which are going to have its transaction injected as a parameter.
        /// </summary>
        /// <param name="method"></param>
        public void AddTransactionInjectionTo(MethodInfo method)
        {
            _transactionalInjectionMethods.Add(method);
        }

        /// <summary>
        /// Gets whether the <see cref="MethodInfo" /> should have its transaction injected
        /// as a parameter.
        /// </summary>
        /// <param name="method">The <see cref="MethodInfo" /> to inject for.</param>
        /// <returns>
        /// Whether to inject the transaction as a parameter into the method invocation.
        /// </returns>
        public bool ShouldInjectTransactionTo(MethodInfo method)
        {
            return _transactionalInjectionMethods.Contains(method);
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
