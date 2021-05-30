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

namespace Castle.Services.Transaction
{
    public sealed class TalkactiveTransaction : TransactionBase, IEventPublisher
    {
        private bool _IsAmbient;
        private bool _IsReadOnly;

        public event EventHandler<TransactionEventArgs> TransactionCompleted;
        public event EventHandler<TransactionFailedEventArgs> TransactionFailed;
        public event EventHandler<TransactionEventArgs> TransactionRolledBack;

        public TalkactiveTransaction(TransactionMode transactionMode, IsolationMode isolationMode, bool isAmbient, bool isReadOnly) :
            base(null, transactionMode, isolationMode)
        {
            _IsAmbient = isAmbient;
            _IsReadOnly = isReadOnly;
        }

        public override bool IsAmbient
        {
            get => _IsAmbient;
            protected set => _IsAmbient = value;
        }

        public override bool IsReadOnly
        {
            get => _IsReadOnly;
            protected set => _IsReadOnly = value;
        }

        public override void Begin()
        {
            try
            {
                base.Begin();
            }
            catch (TransactionException e)
            {
                this.Logger.TryLogFail(() => TransactionFailed.Fire(this, new TransactionFailedEventArgs(this, e)));
                throw;
            }
        }

        protected override void InnerBegin() { }

        public override void Commit()
        {
            try
            {
                base.Commit();
                this.Logger.TryLogFail(() => TransactionCompleted.Fire(this, new TransactionEventArgs(this)));
            }
            catch (TransactionException e)
            {
                this.Logger.TryLogFail(() => TransactionFailed.Fire(this, new TransactionFailedEventArgs(this, e)));
                throw;
            }
        }

        protected override void InnerCommit() { }

        public override void Rollback()
        {
            try
            {
                base.Rollback();
                this.Logger.TryLogFail(() => TransactionRolledBack.Fire(this, new TransactionEventArgs(this)));
            }
            catch (TransactionException e)
            {
                this.Logger.TryLogFail(() => TransactionFailed.Fire(this, new TransactionFailedEventArgs(this, e)));
                throw;
            }
        }

        protected override void InnerRollback() { }
    }
}
