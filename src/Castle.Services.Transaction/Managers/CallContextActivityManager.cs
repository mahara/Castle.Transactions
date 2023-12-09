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

#if NETFRAMEWORK
namespace Castle.Services.Transaction
{
    using System;
    using System.Runtime.Remoting.Messaging;

    public class CallContextActivityManager : MarshalByRefObject, IActivityManager
    {
        private const string Key = "Castle.Services.Transaction.Activity";

        /// <summary>
        /// Initializes a new instance of the <see cref="CallContextActivityManager" /> class.
        /// </summary>
        public CallContextActivityManager()
        {
            CallContext.SetData(Key, null);
        }

        /// <summary>
        /// Gets the current activity.
        /// </summary>
        /// <value>The current activity.</value>
        public Activity CurrentActivity
        {
            get
            {
                var activity = (Activity) CallContext.GetData(Key);

                if (activity == null)
                {
                    activity = new Activity();
                    CallContext.SetData(Key, activity);
                }

                return activity;
            }
        }

        /// <inheritdoc />
        public override object InitializeLifetimeService()
        {
            return null!;
        }
    }
}
#endif
