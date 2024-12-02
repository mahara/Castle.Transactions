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
    public class AsyncLocalActivityManager : MarshalByRefObject, IActivityManager
    {
        private readonly
#if NET9_0_OR_GREATER
            Lock
#else
            object
#endif
            _lock = new();

        private readonly AsyncLocal<Activity> _data = new();

        public AsyncLocalActivityManager()
        {
            _data.Value = null!;
        }

        public Activity CurrentActivity
        {
            get
            {
                Activity activity;

                if ((activity = _data.Value!) is null)
                {
                    lock (_lock)
                    {
                        if ((activity = _data.Value!) is null)
                        {
                            activity = new Activity();

                            _data.Value = activity;
                        }
                    }
                }

                return activity;
            }
        }

#if NET
        //[Obsolete("Obsoletions.RemotingApisMessage, DiagnosticId = Obsoletions.RemotingApisDiagId, UrlFormat = Obsoletions.SharedUrlFormat")]
        [Obsolete("This Remoting API is not supported and throws PlatformNotSupportedException.", DiagnosticId = "SYSLIB0010", UrlFormat = "https://aka.ms/dotnet-warnings/{0}")]
#endif
        public override object InitializeLifetimeService()
        {
            return null!;
        }
    }
}
