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

namespace Castle.Services.Transaction.Tests
{
    using System;

    public class ThrowsExceptionResourceImpl : ResourceImpl
    {
        private readonly bool _throwOnCommit = false;
        private readonly bool _throwOnRollback = false;

        public ThrowsExceptionResourceImpl(bool throwOnCommit, bool throwOnRollback)
        {
            _throwOnCommit = throwOnCommit;
            _throwOnRollback = throwOnRollback;
        }

        public override void Commit()
        {
            if (_throwOnCommit)
            {
                throw new Exception("Simulated commit error.");
            }

            base.Commit();
        }

        public override void Rollback()
        {
            if (_throwOnRollback)
            {
                throw new Exception("Simulated rollback error.");
            }

            base.Rollback();
        }
    }
}
