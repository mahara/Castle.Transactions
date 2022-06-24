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
    using System.Threading;

    using Castle.Core.Logging;

    using Core;

    /// <summary>
    /// Utility class for whatever is needed to make the code better.
    /// </summary>
    internal static class Fun
    {
        public static void Fire<TEventArgs>(this EventHandler<TEventArgs> handler,
                                            object sender,
                                            TEventArgs args)
            where TEventArgs : EventArgs
        {
            if (handler == null)
            {
                return;
            }

            handler(sender, args);
        }

        public static void AtomicRead(this ReaderWriterLockSlim sem, Action action)
        {
            AtomicRead(sem, action, false);
        }

        public static void AtomicRead(this ReaderWriterLockSlim sem, Action action, bool upgradable)
        {
            if (sem == null)
            {
                throw new ArgumentNullException(nameof(sem));
            }

            if (action == null)
            {
                throw new ArgumentNullException("a");
            }

            if (!upgradable)
            {
                sem.EnterReadLock();
            }
            else
            {
                sem.EnterUpgradeableReadLock();
            }

            try
            {
                action();
            }
            finally
            {
                if (!upgradable)
                {
                    sem.ExitReadLock();
                }
                else
                {
                    sem.ExitUpgradeableReadLock();
                }
            }
        }

        public static T AtomicRead<T>(this ReaderWriterLockSlim sem, Func<T> function)
        {
            if (sem == null)
            {
                throw new ArgumentNullException(nameof(sem));
            }

            if (function == null)
            {
                throw new ArgumentNullException("f");
            }

            sem.EnterReadLock();

            try
            {
                return function();
            }
            finally
            {
                sem.ExitReadLock();
            }
        }

        public static void AtomicWrite(this ReaderWriterLockSlim sem, Action action)
        {
            if (sem == null)
            {
                throw new ArgumentNullException(nameof(sem));
            }

            if (action == null)
            {
                throw new ArgumentNullException("a");
            }

            sem.EnterWriteLock();

            try
            {
                action();
            }
            finally
            {
                sem.ExitWriteLock();
            }
        }

        /// <summary>
        /// Iterates over a sequence and performs the action on each.
        /// </summary>
        public static void ForEach<T>(this IEnumerable<T> items, Action<T> action)
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            foreach (var item in items)
            {
                action(item);
            }
        }

        /// <summary>
        /// Given a logger and action, performs the action and catches + logs exceptions.
        /// </summary>
        /// <returns>Whether the lambda was a success or not.</returns>
        public static Error TryLogFail(this ILogger logger, Action action)
        {
            try
            {
                action();

                return Error.OK;
            }
            catch (Exception e)
            {
                logger.Error(e.Message, e);

                return new Error(false, e);
            }
        }

        public static Pair<T, T2> And<T, T2>(this T first, T2 second)
        {
            return new Pair<T, T2>(first, second);
        }
    }

    /// <summary>
    /// Error monad
    /// </summary>
    internal struct Error
    {
        public static Error OK = new Error(true, null);

        private readonly Exception _exception;
        private readonly bool _success;

        public Error(bool success, Exception exception)
        {
            _success = success;
            _exception = success ? null : exception;
        }

        /// <summary>
        /// Takes a lambda what to do if the result failed. Returns the result so
        /// that it can be managed in whatevery way is needed.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public Error Exception(Action<Exception> action)
        {
            if (!_success)
            {
                action(_exception);
            }

            return this;
        }

        public Error Success(Action action)
        {
            if (_success)
            {
                action();
            }

            return this;
        }
    }

    /// <summary>
    /// Error monad
    /// </summary>
    /// <typeparam name="T">Encapsulated success-action parameter type</typeparam>
    internal struct Error<T>
    {
        private readonly Exception _exception;
        private readonly bool _success;
        private readonly T _parameter;

        public Error(bool success, Exception exception, T parameter)
        {
            _success = success;
            _parameter = parameter;
            _exception = success ? null : exception;
        }

        /// <summary>
        /// Takes a lambda what to do if the result failed. Returns the result so
        /// that it can be managed in whatevery way is needed.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public Error<T> Exception(Action<Exception> action)
        {
            if (!_success)
            {
                action(_exception);
            }

            return this;
        }

        public Error<T> Success(Action<T> action)
        {
            if (_success)
            {
                action(_parameter);
            }

            return this;
        }
    }
}