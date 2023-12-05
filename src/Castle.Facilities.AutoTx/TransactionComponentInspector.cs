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

using Castle.Core;
using Castle.Core.Configuration;

using Castle.MicroKernel;
using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.ModelBuilder.Inspectors;

using Castle.Services.Transaction;

namespace Castle.Facilities.AutoTx
{
    /// <summary>
    /// A <see cref="MethodMetaInspector" /> for obtaining transaction configuration
    /// based on the component configuration, or (if not available) check for the attributes.
    /// </summary>
    public class TransactionComponentInspector : MethodMetaInspector
    {
        public const string Transaction_ConfigurationElementName = "transaction";

        private TransactionMetaInfoStore _metaInfoStore;

        /// <summary>
        /// Obtains transaction configuration based on the component configuration,
        /// or (if not available) check for the attributes.
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        /// <param name="model">The model.</param>
        public override void ProcessModel(IKernel kernel, ComponentModel model)
        {
            _metaInfoStore ??= kernel.Resolve<TransactionMetaInfoStore>();

            if (IsMarkedAsTransactional(model.Configuration))
            {
                base.ProcessModel(kernel, model);
            }
            else
            {
                AssertNoTransactionOnConfiguration(model);

                ConfigureBasedOnAttributes(model);
            }

            Validate(model, _metaInfoStore);

            AddTransactionInterceptorIfIsTransactional(model, _metaInfoStore);
        }

        /// <summary>
        /// Obtains the name of the node on the configuration.
        /// </summary>
        /// <returns>The node name on the configuration.</returns>
        protected override string ObtainNodeName()
        {
            return Transaction_ConfigurationElementName;
        }

        /// <summary>
        /// Processes the <see cref="TransactionMetaInfo" /> available on the component configuration.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="methods">The methods.</param>
        /// <param name="metaModel">The meta model.</param>
        protected override void ProcessMeta(ComponentModel model, IList<MethodInfo> methods, MethodMetaModel metaModel)
        {
            _metaInfoStore.CreateMetaInfoFromConfiguration(model.Implementation, methods, metaModel.ConfigNode);
        }

        /// <summary>
        /// Configures the <see cref="ComponentModel" /> based on <see cref="TransactionalAttribute" />.
        /// </summary>
        /// <param name="model">The model.</param>
        private void ConfigureBasedOnAttributes(ComponentModel model)
        {
            if (model.Implementation.IsDefined(typeof(TransactionalAttribute), true))
            {
                _metaInfoStore.CreateMetaInfoFrom(model.Implementation);
            }
        }

        /// <summary>
        /// Validates the ability of the types of the methods to generate a proxy.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="metaInfoStore">The meta-information store.</param>
        private static void Validate(ComponentModel model, TransactionMetaInfoStore metaInfoStore)
        {
            TransactionMetaInfo metaInfo;

            var problematicMethodNames = new List<string>();

            foreach (var service in model.Services)
            {
                if (service is null ||
                    service.IsInterface ||
                    (metaInfo = metaInfoStore.GetMetaInfoFor(model.Implementation)) is null ||
                    (problematicMethodNames = (from method in metaInfo.TransactionalMethods
                                               where !method.IsVirtual
                                               select method.Name)
                                               .ToList())
                    .Count == 0)
                {
                    return;
                }
            }

            var problematicMethodNamesString = string.Join(", ", problematicMethodNames.ToArray());
            throw new FacilityException(
                $"The class '{model.Implementation.FullName}' wants to use transaction interception, " +
                $"however the methods must be marked as virtual in order to do so. " +
                $"Please correct the following methods: '{problematicMethodNamesString}'.");
        }

        /// <summary>
        /// Determines whether the <see cref="IConfiguration" /> has <c>isTransactional="true"</c> attribute.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <returns><see langword="true" /> if yes; otherwise, <see langword="false" />.</returns>
        private static bool IsMarkedAsTransactional(IConfiguration configuration)
        {
            return configuration is not null &&
                   string.Equals(configuration.Attributes["isTransactional"], "true", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Asserts that if there are transaction behavior configured for the methods,
        /// the component node has <c>isTransactional="true"</c> attribute set.
        /// </summary>
        /// <param name="model">The model.</param>
        private static void AssertNoTransactionOnConfiguration(ComponentModel model)
        {
            var configuration = model.Configuration;

            if (configuration is not null && configuration.Children[Transaction_ConfigurationElementName] is not null)
            {
                throw new FacilityException(
                    $"The class '{model.Implementation.FullName}' has configured transaction in a child node, " +
                    $"but has not specified 'isTransactional=\"true\"' on the component node.");
            }
        }

        /// <summary>
        /// Associates the <see cref="TransactionInterceptor" /> with the <see cref="ComponentModel" />.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="metaInfoStore">The meta-information store.</param>
        private void AddTransactionInterceptorIfIsTransactional(
            ComponentModel model,
            TransactionMetaInfoStore metaInfoStore)
        {
            if (metaInfoStore.GetMetaInfoFor(model.Implementation) is null)
            {
                return;
            }

            model.Dependencies.Add(
                new DependencyModel(ObtainNodeName(), typeof(TransactionInterceptor), false));

            model.Interceptors.AddFirst(
                new InterceptorReference(typeof(TransactionInterceptor)));
        }
    }
}
