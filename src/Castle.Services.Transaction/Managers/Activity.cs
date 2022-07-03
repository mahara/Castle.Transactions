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

namespace Castle.Services.Transaction
{
    using System;
    using System.Collections.Generic;

    [Serializable]
    public class Activity : MarshalByRefObject
    {
        private readonly Stack<ITransaction> _transactionStack = new(2);
        private readonly Guid _id = Guid.NewGuid();

        public ITransaction CurrentTransaction =>
            _transactionStack.Count == 0 ? null : _transactionStack.Peek();

        public void Push(ITransaction transaction)
        {
            _transactionStack.Push(transaction);
        }

        public ITransaction Pop()
        {
            return _transactionStack.Pop();
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }

            if (obj is not Activity activity)
            {
                return false;
            }

            return Equals(_id, activity._id);
        }

        public override int GetHashCode()
        {
            return _id.GetHashCode();
        }
    }
}