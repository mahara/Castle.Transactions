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

using Castle.Services.Transaction;

namespace Castle.Facilities.AutoTx
{
    /// <summary>
    /// A storage for attributes found on transactional classes.
    /// </summary>
    public class TransactionMetaInfo : MarshalByRefObject
    {
        private readonly object _lock = new();

        private readonly Dictionary<MethodInfo, TransactionAttribute> _methodToAttribute = [];
        private readonly HashSet<MethodInfo> _methodsToInject = [];
        private readonly Dictionary<MethodInfo, string> _notTransactionalCache = [];

        /// <summary>
        /// Adds a <see cref="MethodInfo" /> and the corresponding <see cref="TransactionAttribute" />.
        /// </summary>
        public void Add(MethodInfo method, TransactionAttribute attribute)
        {
            _methodToAttribute[method] = attribute;
        }

        /// <summary>
        /// Adds the <see cref="MethodInfo" /> to the list of <see cref="MethodInfo" />,
        /// which are going to have their transactions injected as a parameter.
        /// </summary>
        /// <param name="method"></param>
        public void AddInjection(MethodInfo method)
        {
            _methodsToInject.Add(method);
        }

        /// <summary>
        /// A collection of <see cref="MethodInfo" /> which need transaction.
        /// </summary>
        public ICollection<MethodInfo> Methods
        {
            get
            {
                // Quicker than array:
                // https://learn.microsoft.com/en-us/archive/blogs/ricom/performance-quiz-9-ilistlttgt-list-and-array-speed
                // http://blogs.msdn.com/ricom/archive/2006/03/12/549987.aspx
                var methodInfos = new List<MethodInfo>(_methodToAttribute.Count);
                methodInfos.AddRange(_methodToAttribute.Keys);
                return methodInfos;
            }
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
                if (_methodToAttribute.ContainsKey(method))
                {
                    return true;
                }

                if (_notTransactionalCache.ContainsKey(method))
                {
                    return false;
                }

                if ((method.DeclaringType is Type declaringType && declaringType.IsGenericType) ||
                    method.IsGenericMethod)
                {
                    return IsGenericMethodTransactional(method);
                }

                return false;
            }
        }

        /// <summary>
        /// Gets whether the <see cref="MethodInfo" /> should have its transaction injected.
        /// </summary>
        /// <param name="method">The <see cref="MethodInfo" /> to inject for.</param>
        /// <returns>
        /// Whether to inject the transaction as a parameter into the method invocation.
        /// </returns>
        public bool ShouldInject(MethodInfo method)
        {
            return _methodsToInject.Contains(method);
        }

        /// <summary>
        /// Returns the <see cref="TransactionAttribute" /> for a given <see cref="MethodInfo" />.
        /// </summary>
        public TransactionAttribute GetTransactionAttributeFor(MethodInfo method)
        {
            return _methodToAttribute[method];
        }

        private bool IsGenericMethodTransactional(MethodInfo method)
        {
            var attributes = method.GetCustomAttributes(typeof(TransactionAttribute), true);

            if (attributes.Length > 0)
            {
                Add(method, (TransactionAttribute) attributes[0]);

                return true;
            }
            else
            {
                _notTransactionalCache[method] = string.Empty;
            }

            return false;
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
