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

using System;

using Castle.Services.Transaction;

namespace Castle.Facilities.AutoTx.Tests
{
    public class MockTransaction : TransactionBase
    {
        public MockTransaction() :
            base(null, TransactionMode.Unspecified, IsolationMode.Unspecified)
        {
        }

        public override bool IsAmbient
        {
            get => throw new NotImplementedException();
            protected set => throw new NotImplementedException();
        }

        public override bool IsChildTransaction => false;

        public override bool IsReadOnly
        {
            get => throw new NotImplementedException();
            protected set => throw new NotImplementedException();
        }

        protected override void InnerBegin()
        {
        }

        protected override void InnerCommit()
        {
        }

        protected override void InnerRollback()
        {
        }
    }
}
