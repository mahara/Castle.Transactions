#region License
// Copyright 2004-2019 Castle Project - https://www.castleproject.org/
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

namespace Castle.Services.Transaction.Tests
{
    internal class TestResource : ResourceImpl
    {
        private readonly Action _S;
        private readonly Action _C;
        private readonly Action _R;

        public TestResource(Action s, Action c, Action r)
        {
            _S = s;
            _C = c;
            _R = r;
        }

        public override void Start()
        {
            base.Start();
            _S();
        }

        public override void Commit()
        {
            base.Commit();
            _C();
        }

        public override void Rollback()
        {
            base.Rollback();
            _R();
        }
    }
}
