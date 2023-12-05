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

namespace Castle.Services.Transaction.Tests
{
    internal class TestResource : ResourceImpl
    {
        private readonly Action _s;
        private readonly Action _c;
        private readonly Action _r;

        public TestResource(Action s, Action c, Action r)
        {
            _s = s;
            _c = c;
            _r = r;
        }

        public override void Start()
        {
            base.Start();

            _s();
        }

        public override void Commit()
        {
            base.Commit();

            _c();
        }

        public override void Rollback()
        {
            base.Rollback();

            _r();
        }
    }
}
