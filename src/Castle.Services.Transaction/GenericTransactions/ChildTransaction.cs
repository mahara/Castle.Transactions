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

namespace Castle.Services.Transaction
{
    /// <summary>
    /// Emulates a standalone transaction,
    /// but in fact it just propagates a transaction.
    /// </summary>
    public sealed class ChildTransaction : TransactionBase
    {
        private readonly ITransaction _parent;

        public ChildTransaction(ITransaction parent) :
            base($"{nameof(ChildTransaction)} to {parent.Name}",
                 parent.TransactionMode,
                 parent.IsolationLevel)
        {
            _parent = parent;
        }

        public override bool IsAmbient
        {
            get => true;
            protected set { }
        }

        public override bool IsChildTransaction => true;

        public override bool IsReadOnly
        {
            get => _parent.IsReadOnly;
            protected set { }
        }

        public override bool IsRollbackOnlySet => _parent.IsRollbackOnlySet;

        public override void Begin()
        {
        }

        protected override void InnerBegin()
        {
        }

        public override void Commit()
        {
        }

        protected override void InnerCommit()
        {
        }

        public override void Rollback()
        {
            // Vote as rollback.
            _parent.SetRollbackOnly();
        }

        protected override void InnerRollback()
        {
        }

        public override void SetRollbackOnly()
        {
            _parent.SetRollbackOnly();
        }

        public override void Enlist(IResource? resource)
        {
            _parent.Enlist(resource);
        }

        public override void RegisterSynchronization(ISynchronization? synchronization)
        {
            _parent.RegisterSynchronization(synchronization);
        }
    }
}
