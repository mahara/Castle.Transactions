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
    using MicroKernel.Facilities;
    using MicroKernel.Registration;

    using Services.Transaction;
    using Services.Transaction.IO;

    /// <summary>
    /// Augments the kernel to handle transactional components.
    /// </summary>
    public class AutoTxFacility : AbstractFacility
    {

        /// <summary>
        /// Constructor.
        /// </summary>
        public AutoTxFacility()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="allowAccessOutsideRootFolder"><see cref="AllowAccessOutsideRootFolder" /></param>
        /// <param name="rootFolder"></param>
        public AutoTxFacility(bool allowAccessOutsideRootFolder,
                              string rootFolder)
        {
            AllowAccessOutsideRootFolder = allowAccessOutsideRootFolder;
            RootFolder = rootFolder;
        }

        /// <summary>
        /// This triggers a new file adapter / directory adapter to be created.
        /// </summary>
        public bool AllowAccessOutsideRootFolder { get; set; } = true;

        /// <summary>
        /// Gets or sets the root folder for file transactions.
        /// </summary>
        public string RootFolder { get; set; }

        /// <summary>
        /// Registers the interceptor component, the meta-information store, and
        /// adds a contributor to the ModelBuilder
        /// </summary>
        protected override void Init()
        {
            AssertHasDirectories();

            Kernel.Register(
                // Transient components (e.g.: TransactionInterceptor) don't need to be named.
                Component.For<TransactionInterceptor>(),
                Component.For<TransactionMetaInfoStore>().Named("transaction.metaInfoStore"),
                Component.For<IMapPath>().ImplementedBy<MapPathImpl>().Named("directory.adapter.mapPath")
                );

            RegisterAdapters();

            Kernel.ComponentModelBuilder.AddContributor(new TransactionComponentInspector());
        }

        private void RegisterAdapters()
        {
            var directoryAdapter = new DirectoryAdapter(
                Kernel.Resolve<IMapPath>(),
                !AllowAccessOutsideRootFolder,
                RootFolder);
            Kernel.Register(Component.For<IDirectoryAdapter>().Named("directory.adapter").
                            Instance(directoryAdapter));

            var fileAdapter = new FileAdapter(
                !AllowAccessOutsideRootFolder,
                RootFolder);
            Kernel.Register(Component.For<IFileAdapter>().Named("file.adapter")
                            .Instance(fileAdapter));

            if (Kernel.HasComponent(typeof(ITransactionManager)))
            {
                fileAdapter.TransactionManager = directoryAdapter.TransactionManager = Kernel.Resolve<ITransactionManager>();
            }
            else
            {
                Kernel.ComponentRegistered += Kernel_ComponentRegistered;
            }
        }

        private void Kernel_ComponentRegistered(string key, MicroKernel.IHandler handler)
        {
            foreach (var service in handler.ComponentModel.Services)
            {
                if (service.IsAssignableFrom(typeof(ITransactionManager)))
                {
                    var transactionManager = Kernel.Resolve<ITransactionManager>();

                    ((DirectoryAdapter) Kernel.Resolve<IDirectoryAdapter>()).TransactionManager = transactionManager;
                    ((FileAdapter) Kernel.Resolve<IFileAdapter>()).TransactionManager = transactionManager;
                }
            }
        }

        /// <summary>
        /// Disposes the facility.
        /// </summary>
        protected override void Dispose()
        {
            Kernel.ComponentRegistered -= Kernel_ComponentRegistered;

            base.Dispose();
        }

        private void AssertHasDirectories()
        {
            if (!AllowAccessOutsideRootFolder && RootFolder == null)
            {
                throw new FacilityException("You have to specify a root directory.");
            }
        }
    }
}