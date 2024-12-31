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

#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif

namespace Castle.Services.Transaction
{
#if NET
    [Obsolete($"'CallContext' is not supported on .NET (Core). Use '{nameof(ThreadLocalActivityManager)}' instead.")]
#endif
    public class CallContextActivityManager : MarshalByRefObject, IActivityManager
    {
#if NETFRAMEWORK
        private const string Name = "Castle.Services.Transaction.Activity.CallContext";

        private readonly object _lock = new();
#endif

        public CallContextActivityManager()
        {
#if NETFRAMEWORK
            CallContext.SetData(Name, null);
#endif
        }

        public Activity CurrentActivity
        {
            get
            {
#if NETFRAMEWORK
                Activity activity;

                if ((activity = (Activity) CallContext.GetData(Name)) is null)
                {
                    lock (_lock)
                    {
                        if ((activity = (Activity) CallContext.GetData(Name)) is null)
                        {
                            activity = new Activity();

                            CallContext.SetData(Name, activity);
                        }
                    }
                }

                return activity;
#else
                var message = "'CallContext' is not supported on .NET (Core).";
                throw new PlatformNotSupportedException(message);
#endif
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
