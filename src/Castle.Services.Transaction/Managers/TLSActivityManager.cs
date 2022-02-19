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

namespace Castle.Services.Transaction
{
    /// <summary>
    /// </summary>
    /// <remarks>
    /// REFERENCES:
    /// -   <see href="https://learn.microsoft.com/en-us/dotnet/standard/threading/thread-local-storage-thread-relative-static-fields-and-data-slots" />
    /// -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.threadstaticattribute" />
    /// -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.localdatastoreslot" />
    /// </remarks>
    [Obsolete($"Use '{nameof(ThreadLocalActivityManager)}' instead.")]
    public class TLSActivityManager : MarshalByRefObject, IActivityManager
    {
        [ThreadStatic]
        private static Activity _activity;

        private readonly object _lock = new();

        public Activity CurrentActivity
        {
            get
            {
                if (_activity == null)
                {
                    lock (_lock)
                    {
                        if (_activity == null)
                        {
                            _activity = new Activity();
                        }
                    }
                }

                return _activity;
            }
        }

        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
