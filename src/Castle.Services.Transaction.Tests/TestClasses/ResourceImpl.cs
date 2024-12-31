#region License
// Copyright 2004-2025 Castle Project - https://www.castleproject.org/
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
    public class ResourceImpl : IResource, IDisposable
    {
        public bool WasDisposed;

        public bool Started { get; private set; }

        public bool Committed { get; private set; }

        public bool Rolledback { get; private set; }

        public void Dispose()
        {
            WasDisposed = true;

            GC.SuppressFinalize(this);
        }

        public virtual void Start()
        {
            if (Started)
            {
                throw new ApplicationException("Start called before.");
            }

            Started = true;
        }

        public virtual void Commit()
        {
            if (Committed)
            {
                throw new ApplicationException("Commit called before.");
            }

            Committed = true;
        }

        public virtual void Rollback()
        {
            if (Rolledback)
            {
                throw new ApplicationException("Rollback called before.");
            }

            Rolledback = true;
        }
    }
}
