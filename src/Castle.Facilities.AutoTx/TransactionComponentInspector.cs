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

namespace Castle.Facilities.AutoTx
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    using Core;
    using Core.Configuration;

    using MicroKernel;
    using MicroKernel.Facilities;
    using MicroKernel.ModelBuilder.Inspectors;

    using Services.Transaction;

    /// <summary>
    /// Tries to obtain transaction configuration based on the component configuration,
    /// or (if not available) check for the attributes.
    /// </summary>
    public class TransactionComponentInspector : MethodMetaInspector
    {
        private static readonly string TransactionNodeName = "transaction";

        private TransactionMetaInfoStore _metaInfoStore;

        /// <summary>
        /// Tries to obtain transaction configuration based on the component configuration
        /// or (if not available) check for the attributes.
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        /// <param name="model">The model.</param>
        public override void ProcessModel(IKernel kernel, ComponentModel model)
        {
            _metaInfoStore ??= kernel.Resolve<TransactionMetaInfoStore>();

            if (IsMarkedWithTransactional(model.Configuration))
            {
                base.ProcessModel(kernel, model);
            }
            else
            {
                AssertThereNoTransactionOnConfig(model);

                ConfigureBasedOnAttributes(model);
            }

            Validate(model, _metaInfoStore);

            AddTransactionInterceptorIfIsTransactional(model, _metaInfoStore);
        }

        /// <summary>
        /// Tries to configure the <see cref="ComponentModel" /> based on attributes.
        /// </summary>
        /// <param name="model">The model.</param>
        private void ConfigureBasedOnAttributes(ComponentModel model)
        {
            if (model.Implementation.IsDefined(typeof(TransactionalAttribute), true))
            {
                _metaInfoStore.CreateMetaInfoFromType(model.Implementation);
            }
        }

        /// <summary>
        /// Obtains the name of the node (overrides MethodMetaInspector.ObtainNodeName)
        /// </summary>
        /// <returns>the node name on the configuration</returns>
        protected override string ObtainNodeName()
        {
            return TransactionNodeName;
        }

        /// <summary>
        /// Processes the meta-information available on the component configuration.
        /// (overrides MethodMetaInspector.ProcessMeta)
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="methods">The methods.</param>
        /// <param name="metaModel">The meta model.</param>
        protected override void ProcessMeta(ComponentModel model, IList<MethodInfo> methods, MethodMetaModel metaModel)
        {
            _metaInfoStore.CreateMetaInfoFromConfig(model.Implementation, methods, metaModel.ConfigNode);
        }

        /// <summary>
        /// Validates the type is OK to generate a proxy.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="store">The store.</param>
        private static void Validate(ComponentModel model, TransactionMetaInfoStore store)
        {
            TransactionMetaInfo metaInfo;

            var problematicMethods = new List<string>();

            foreach (var service in model.Services)
            {
                if (service == null ||
                    service.IsInterface ||
                    (metaInfo = store.GetMetaInfoFor(model.Implementation)) == null ||
                     (problematicMethods = (from method in metaInfo.Methods
                                            where !method.IsVirtual
                                            select method.Name)
                                           .ToList())
                     .Count == 0)
                {
                    return;
                }
            }

            throw new FacilityException(
                string.Format(
                    "The class {0} wants to use transaction interception, " +
                    "however the methods must be marked as virtual in order to do so. " +
                    "Please correct the following methods: {1}.",
                    model.Implementation.FullName,
                    string.Join(", ", problematicMethods.ToArray())));
        }

        /// <summary>
        /// Determines whether the configuration has <c>isTransaction="true"</c> attribute.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns>
        /// <c>true</c> if yes; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsMarkedWithTransactional(IConfiguration configuration)
        {
            return configuration != null &&
                   configuration.Attributes["isTransactional"] == "true";
        }

        /// <summary>
        /// Asserts that if there are transaction behavior
        /// configured for methods, the component node has <c>isTransaction="true"</c> attribute
        /// </summary>
        /// <param name="model">The model.</param>
        private static void AssertThereNoTransactionOnConfig(ComponentModel model)
        {
            var configuration = model.Configuration;

            if (configuration != null && configuration.Children[TransactionNodeName] != null)
            {
                throw new FacilityException(
                    string.Format(
                        "The class {0} has configured transaction in a child node " +
                        "but has not specified isTransaction=\"true\" on the component node.",
                        model.Implementation.FullName));
            }
        }

        /// <summary>
        /// Associates the transaction interceptor with the <see cref="ComponentModel" />.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="store">The meta-information store.</param>
        private void AddTransactionInterceptorIfIsTransactional(ComponentModel model,
                                                                TransactionMetaInfoStore store)
        {
            var metaInfo = store.GetMetaInfoFor(model.Implementation);
            if (metaInfo == null)
            {
                return;
            }

            model.Dependencies.Add(
                new DependencyModel(ObtainNodeName(), typeof(TransactionInterceptor), false));

            model.Interceptors.AddFirst(new InterceptorReference(typeof(TransactionInterceptor)));
        }
    }
}
