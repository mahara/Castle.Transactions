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

using Castle.MicroKernel.Facilities;
using Castle.MicroKernel.Registration;

using Castle.Services.Transaction;
using Castle.Services.Transaction.IO;

namespace Castle.Facilities.AutoTx
{
    /// <summary>
    /// Augments the kernel to handle transactional components.
    /// </summary>
    public class AutoTxFacility : AbstractFacility
    {
        //public const string TransactionInterceptor_ComponentName = "transaction.interceptor";
        //public const string TransactionMetaInfoStore_ComponentName = "transaction.metainfostore";
        //public const string DirectoryAdapterPathMapper_ComponentName = "directory.adapter.pathmapper";
        //public const string DirectoryAdapter_ComponentName = "directory.adapter";
        //public const string FileAdapter_ComponentName = "file.adapter";

        public AutoTxFacility()
        {
        }

        public AutoTxFacility(bool allowAccessOutsideRootDirectory,
                              string rootDirectory)
        {
            AllowAccessOutsideRootDirectory = allowAccessOutsideRootDirectory;
            RootDirectory = rootDirectory;
        }

        protected override void Dispose()
        {
            Kernel.ComponentRegistered -= Kernel_ComponentRegistered;

            base.Dispose();
        }

        /// <summary>
        /// This triggers a new directory/file adapter to be created.
        /// </summary>
        public bool AllowAccessOutsideRootDirectory { get; set; } = true;

        /// <summary>
        /// Gets or sets the root directory for file transactions.
        /// </summary>
        public string RootDirectory { get; set; }

        /// <summary>
        /// Registers the <see cref="TransactionInterceptor" />,
        /// the <see cref="TransactionMetaInfoStore" />,
        /// and adds a contributor to the ModelBuilder.
        /// </summary>
        protected override void Init()
        {
            AssertHasDirectories();

            //
            // NOTE:    Naming the following components using Named() method,
            //          especially TransactionInterceptor,
            //          will cause property dependencies of a resolved instance
            //          not being injected in NHibernateFacility.
            //
            Kernel.Register(
                //Component.For<TransactionInterceptor>()
                //         .Named(TransactionInterceptor_ComponentName),
                Component.For<TransactionInterceptor>(),
                //Component.For<TransactionMetaInfoStore>()
                //         .Named(TransactionMetaInfoStore_ComponentName),
                Component.For<TransactionMetaInfoStore>(),
                //Component.For<IPathMapper>()
                //         .ImplementedBy<PathMapper>()
                //         .Named(DirectoryAdapterPathMapper_ComponentName));
                Component.For<IPathMapper>()
                         .ImplementedBy<PathMapper>());

            RegisterAdapters();

            Kernel.ComponentModelBuilder.AddContributor(new TransactionComponentInspector());
        }

        private void RegisterAdapters()
        {
            var directoryAdapter = new DirectoryAdapter(
                Kernel.Resolve<IPathMapper>(),
                !AllowAccessOutsideRootDirectory,
                RootDirectory);

            Kernel.Register(
                //Component.For<IDirectoryAdapter>()
                //         .Named(DirectoryAdapter_ComponentName)
                //         .Instance(directoryAdapter));
                Component.For<IDirectoryAdapter>()
                         .Instance(directoryAdapter));

            var fileAdapter = new FileAdapter(
                !AllowAccessOutsideRootDirectory,
                RootDirectory);
            Kernel.Register(
                //Component.For<IFileAdapter>()
                //         .Named(FileAdapter_ComponentName)
                //         .Instance(fileAdapter));
                Component.For<IFileAdapter>()
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
                    var manager = Kernel.Resolve<ITransactionManager>();

                    ((DirectoryAdapter) Kernel.Resolve<IDirectoryAdapter>()).TransactionManager = manager;
                    ((FileAdapter) Kernel.Resolve<IFileAdapter>()).TransactionManager = manager;
                }
            }
        }

        private void AssertHasDirectories()
        {
            if (!AllowAccessOutsideRootDirectory && RootDirectory is null)
            {
                throw new FacilityException("You have to specify a root directory.");
            }
        }
    }
}
