#region License
// Copyright 2004-2021 Castle Project - https://www.castleproject.org/
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

using System;

using Castle.MicroKernel.Facilities;
using Castle.Windsor;

using NUnit.Framework;

namespace Castle.Facilities.AutoTx.Tests
{
    [TestFixture]
    public class ConfiguredComponentsTests
    {
        [Test]
        public void IsTransactionalMissing()
        {
            static void Method()
            {
                _ = new WindsorContainer("IsTransactionalMissing.xml");
            }

            Assert.That(
                Method,
                Throws.TypeOf<FacilityException>()
                      .And.Message.EqualTo("The class 'Castle.Facilities.AutoTx.Tests.TransactionalService2' has configured transaction in a child node, but has not specified 'isTransactional=\"true\"' on the component node."));
        }

        [Test]
        public void HasIsTransactionalButNothingIsConfigured()
        {
            var container = new WindsorContainer("HasIsTransactionalButNothingIsConfigured.xml");

            var metaInfoStore = container.Resolve<TransactionMetaInfoStore>();

            var metaInfo = metaInfoStore.GetMetaInfoFor(typeof(TransactionalService2));

            Assert.That(metaInfo, Is.Null);
        }

        [Test]
        public void HasConfiguration()
        {
            var container = new WindsorContainer("HasConfiguration.xml");

            var metaInfoStore = container.Resolve<TransactionMetaInfoStore>();

            var metaInfo = metaInfoStore.GetMetaInfoFor(typeof(TransactionalService2));

            Assert.That(metaInfo, Is.Not.Null);
            Assert.That(metaInfo.TransactionalMethods.Count, Is.EqualTo(3));
        }

        [Test]
        public void HasInvalidMethod()
        {
            static void Method()
            {
                _ = new WindsorContainer("HasInvalidMethod.xml");
            }

            Assert.That(
                Method,
                Throws.TypeOf<Exception>()
                      .And.Message.EqualTo("The class Castle.Facilities.AutoTx.Tests.TransactionalService2 has tried to expose configuration for a method named HelloGoodbye which could not be found."));
        }

        [Test]
        public void ValidConfigForInheritedMethods()
        {
            var container = new WindsorContainer("ValidConfigForInheritedMethods.xml");

            var metaInfoStore = container.Resolve<TransactionMetaInfoStore>();

            var metaInfo = metaInfoStore.GetMetaInfoFor(typeof(TransactionalService21));

            Assert.That(metaInfo, Is.Not.Null);
            Assert.That(metaInfo.TransactionalMethods.Count, Is.EqualTo(4));
        }

        [Test]
        public void ConfigForServiceWithInterface()
        {
            var container = new WindsorContainer("ConfigForServiceWithInterface.xml");

            var metaInfoStore = container.Resolve<TransactionMetaInfoStore>();

            var metaInfo = metaInfoStore.GetMetaInfoFor(typeof(TransactionalService1));

            Assert.That(metaInfo, Is.Not.Null);
            Assert.That(metaInfo.TransactionalMethods.Count, Is.EqualTo(2));
        }
    }
}
