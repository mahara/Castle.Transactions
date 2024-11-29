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

using Castle.Core;
using Castle.Core.Interceptor;
using Castle.Core.Logging;

using Castle.DynamicProxy;

using Castle.MicroKernel;

using Castle.Services.Transaction;

namespace Castle.Facilities.AutoTx
{
    /// <summary>
    /// Intercepts call for transactional components, coordinating the transaction creation,
    /// commit/rollback accordingly to the method execution.
    /// Rollback is invoked if an exception is thrown.
    /// </summary>
    [Transient]
    public class TransactionInterceptor : IInterceptor, IOnBehalfAware
    {
        private readonly IKernel _kernel;
        private readonly TransactionMetaInfoStore _metaInfoStore;

        private TransactionMetaInfo? _metaInfo;

        /// <summary>
        /// Initializes a new instance of the <see cref="TransactionInterceptor" /> class.
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        /// <param name="metaInfoStore">The meta-information store.</param>
        public TransactionInterceptor(IKernel kernel, TransactionMetaInfoStore metaInfoStore)
        {
            _kernel = kernel;
            _metaInfoStore = metaInfoStore;
        }

        public ILogger Logger { get; set; } = NullLogger.Instance;

        /// <summary>
        /// Sets the intercepted component's <see cref="ComponentModel" />.
        /// </summary>
        /// <param name="target">The targeted model.</param>
        public void SetInterceptedComponentModel(ComponentModel target)
        {
            _metaInfo = _metaInfoStore.GetMetaInfoFor(target.Implementation);
        }

        /// <summary>
        /// Intercepts the specified invocation and creates a transaction if necessary.
        /// </summary>
        /// <param name="invocation">The invocation.</param>
        /// <returns></returns>
        public void Intercept(IInvocation invocation)
        {
            MethodInfo method;

            if (invocation.Method.DeclaringType is Type declaringType && declaringType.IsInterface)
            {
                method = invocation.MethodInvocationTarget;
            }
            else
            {
                method = invocation.Method;
            }

            if (_metaInfo is null || !_metaInfo.IsMethodTransactional(method))
            {
                invocation.Proceed();

                return;
            }

            var attribute = _metaInfo.GetTransactionAttributeFor(method);
            var manager = _kernel.Resolve<ITransactionManager>();
            var transaction = manager.CreateTransaction(attribute.TransactionMode,
                                                        attribute.IsolationLevel,
                                                        attribute.IsDistributed,
                                                        attribute.IsReadOnly);

            if (transaction is null)
            {
                invocation.Proceed();

                return;
            }

            transaction.Begin();

            var isRolledback = false;

            try
            {
                if (_metaInfo.ShouldInject(method))
                {
                    var parameters = method.GetParameters();

                    for (var i = 0; i < parameters.Length; i++)
                    {
                        if (parameters[i].ParameterType == typeof(ITransaction))
                        {
                            invocation.SetArgumentValue(i, transaction);
                        }
                    }
                }

                invocation.Proceed();

                if (transaction.IsRollbackOnlySet)
                {
                    Logger.Debug($"Rolling back transaction '{transaction.GetHashCode()}'.");

                    isRolledback = true;
                    transaction.Rollback();
                }
                else
                {
                    Logger.Debug($"Committing transaction '{transaction.GetHashCode()}'.");

                    transaction.Commit();
                }
            }
            catch (TransactionException ex)
            {
                // Whoops.
                // Special case: let's throw without attempt to rollback anything.

                if (Logger.IsFatalEnabled)
                {
                    Logger.Fatal("Fatal error during transaction processing.", ex);
                }

                throw;
            }
            catch (Exception)
            {
                if (!isRolledback)
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug($"Rolling back transaction '{transaction.GetHashCode()}' due to exception on method '{method.DeclaringType?.Name}.{method.Name}'.");
                    }

                    transaction.Rollback();
                }

                throw;
            }
            finally
            {
                manager.Dispose(transaction);
            }
        }
    }
}
