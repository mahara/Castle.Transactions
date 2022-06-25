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
using System.Transactions;

namespace Castle.Services.Transaction
{
    public sealed class TalkactiveTransaction : TransactionBase, IEventPublisher
    {
        private bool _isAmbient;
        private bool _isReadOnly;

        public TalkactiveTransaction(TransactionMode transactionMode,
                                     IsolationLevel isolationMode,
                                     bool isAmbient,
                                     bool isReadOnly) :
            base(null, transactionMode, isolationMode)
        {
            _isAmbient = isAmbient;
            _isReadOnly = isReadOnly;
        }

        public event EventHandler<TransactionEventArgs> TransactionCompleted;
        public event EventHandler<TransactionEventArgs> TransactionRolledBack;
        public event EventHandler<TransactionFailedEventArgs> TransactionFailed;

        public override bool IsAmbient
        {
            get => _isAmbient;
            protected set => _isAmbient = value;
        }

        public override bool IsReadOnly
        {
            get => _isReadOnly;
            protected set => _isReadOnly = value;
        }

        public override void Begin()
        {
            try
            {
                base.Begin();
            }
            catch (TransactionException ex)
            {
                Logger.TryLogFail(() => TransactionFailed.Fire(this, new TransactionFailedEventArgs(this, ex)));

                throw;
            }
        }

        protected override void InnerBegin() { }

        public override void Commit()
        {
            try
            {
                base.Commit();

                Logger.TryLogFail(() => TransactionCompleted.Fire(this, new TransactionEventArgs(this)));
            }
            catch (TransactionException ex)
            {
                Logger.TryLogFail(() => TransactionFailed.Fire(this, new TransactionFailedEventArgs(this, ex)));

                throw;
            }
        }

        protected override void InnerCommit() { }

        public override void Rollback()
        {
            try
            {
                base.Rollback();

                Logger.TryLogFail(() => TransactionRolledBack.Fire(this, new TransactionEventArgs(this)));
            }
            catch (TransactionException ex)
            {
                Logger.TryLogFail(() => TransactionFailed.Fire(this, new TransactionFailedEventArgs(this, ex)));

                throw;
            }
        }

        protected override void InnerRollback() { }
    }
}
