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

    using Services.Transaction;

    /// <summary>
    /// Storage for attributes found on transactional classes.
    /// </summary>
    public class TransactionMetaInfo : MarshalByRefObject
    {
        private readonly object _lock = new object();
        private readonly Dictionary<MethodInfo, TransactionAttribute> _methodToAttribute;
        private readonly HashSet<MethodInfo> _injectMethods;
        private readonly Dictionary<MethodInfo, string> _notTransactionalCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionMetaInfo" /> class.
        /// </summary>
        public TransactionMetaInfo()
        {
            _methodToAttribute = new Dictionary<MethodInfo, TransactionAttribute>();
            _injectMethods = new HashSet<MethodInfo>();
            _notTransactionalCache = new Dictionary<MethodInfo, string>();
        }

        #region MarshalByRefObject overrides

        /// <summary>
        /// Obtains a lifetime service object to control the lifetime policy for this instance.
        /// </summary>
        /// <returns>
        /// An object of type <see cref="T:System.Runtime.Remoting.Lifetime.ILease" /> used to control the lifetime policy for this instance. This is the current lifetime service object for this instance if one exists; otherwise, a new lifetime service object initialized to the value of the <see cref="P:System.Runtime.Remoting.Lifetime.LifetimeServices.LeaseManagerPollTime" /> property.
        /// </returns>
        /// <exception cref="T:System.Security.SecurityException">The immediate caller does not have infrastructure permission.
        ///                 </exception><filterpriority>2</filterpriority><PermissionSet><IPermission class="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089" version="1" Flags="RemotingConfiguration, Infrastructure" /></PermissionSet>
        public override object InitializeLifetimeService()
        {
            return null;
        }

        #endregion

        /// <summary>
        /// Adds a method info and the corresponding transaction attribute.
        /// </summary>
        public void Add(MethodInfo method, TransactionAttribute attribute)
        {
            _methodToAttribute[method] = attribute;
        }

        /// <summary>
        /// Adds the method to the list of method,
        /// which are going to have their transactions injected as a parameter.
        /// </summary>
        /// <param name="method"></param>
        public void AddInjection(MethodInfo method)
        {
            _injectMethods.Add(method);
        }

        /// <summary>
        /// Methods which needs transactions.
        /// </summary>
        public IEnumerable<MethodInfo> Methods
        {
            get
            {
                // Quicker than array: http://blogs.msdn.com/ricom/archive/2006/03/12/549987.aspx
                // Quicker than array: https://docs.microsoft.com/en-us/archive/blogs/ricom/performance-quiz-9-ilistlttgt-list-and-array-speed
                var methods = new List<MethodInfo>(_methodToAttribute.Count);
                methods.AddRange(_methodToAttribute.Keys);
                return methods;
            }
        }

        /// <summary>
        /// True if methods is transactional. Otherwise, False.
        /// </summary>
        public bool Contains(MethodInfo info)
        {
            lock (_lock)
            {
                if (_methodToAttribute.ContainsKey(info))
                {
                    return true;
                }

                if (_notTransactionalCache.ContainsKey(info))
                {
                    return false;
                }

                if (info.DeclaringType.IsGenericType || info.IsGenericMethod)
                {
                    return IsGenericMethodTransactional(info);
                }

                return false;
            }
        }

        /// <summary>
        /// Gets whether the method should have its transaction injected.
        /// </summary>
        /// <param name="info">The method to inject for.</param>
        /// <returns>Whether to inject the transaction as a parameter into the method invocation.</returns>
        public bool ShouldInject(MethodInfo info)
        {
            return _injectMethods.Contains(info);
        }

        /// <summary>
        /// Returns the transaction metadata for a given method.
        /// </summary>
        public TransactionAttribute GetTransactionAttributeFor(MethodInfo methodInfo)
        {
            return _methodToAttribute[methodInfo];
        }

        private bool IsGenericMethodTransactional(MethodInfo info)
        {
            var atts = info.GetCustomAttributes(typeof(TransactionAttribute), true);

            if (atts.Length != 0)
            {
                Add(info, atts[0] as TransactionAttribute);

                return true;
            }
            else
            {
                _notTransactionalCache[info] = string.Empty;
            }

            return false;
        }
    }
}