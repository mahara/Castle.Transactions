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

using Castle.Core.Logging;

namespace Castle.Services.Transaction.Utilities
{
    /// <summary>
    /// Utility class for whatever is needed to make the code better.
    /// </summary>
    internal static class Utility
    {
        public static void Fire<TEventArgs>(this EventHandler<TEventArgs>? handler,
                                            object? sender,
                                            TEventArgs args)
            where TEventArgs : EventArgs
        {
            if (handler is null)
            {
                return;
            }

            handler(sender, args);
        }

        public static void AtomicRead(this ReaderWriterLockSlim @lock, Action action)
        {
            AtomicRead(@lock, action, false);
        }

        public static void AtomicRead(this ReaderWriterLockSlim @lock, Action action, bool upgradable)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(@lock);
            ArgumentNullException.ThrowIfNull(action);
#else
            if (@lock is null)
            {
                throw new ArgumentNullException(nameof(@lock));
            }
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }
#endif

            if (!upgradable)
            {
                @lock.EnterReadLock();
            }
            else
            {
                @lock.EnterUpgradeableReadLock();
            }

            try
            {
                action();
            }
            finally
            {
                if (!upgradable)
                {
                    @lock.ExitReadLock();
                }
                else
                {
                    @lock.ExitUpgradeableReadLock();
                }
            }
        }

        public static T AtomicRead<T>(this ReaderWriterLockSlim @lock, Func<T> function)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(@lock);
            ArgumentNullException.ThrowIfNull(function);
#else
            if (@lock is null)
            {
                throw new ArgumentNullException(nameof(@lock));
            }
            if (function is null)
            {
                throw new ArgumentNullException(nameof(function));
            }
#endif

            @lock.EnterReadLock();

            try
            {
                return function();
            }
            finally
            {
                @lock.ExitReadLock();
            }
        }

        public static void AtomicWrite(this ReaderWriterLockSlim @lock, Action action)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(@lock);
            ArgumentNullException.ThrowIfNull(action);
#else
            if (@lock is null)
            {
                throw new ArgumentNullException(nameof(@lock));
            }
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }
#endif

            @lock.EnterWriteLock();

            try
            {
                action();
            }
            finally
            {
                @lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Iterates over a sequence and performs the action on each element.
        /// </summary>
        public static void ForEach<TSource>(this IEnumerable<TSource> source, Action<TSource> action)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(source);
            ArgumentNullException.ThrowIfNull(action);
#else
            if (source is null)
            {
                throw new ArgumentNullException(nameof(source));
            }
            if (action is null)
            {
                throw new ArgumentNullException(nameof(action));
            }
#endif

            foreach (var element in source)
            {
                action(element);
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
            catch (Exception ex)
            {
                logger.Error(ex.Message, ex);

                return new Error(false, ex);
            }
        }
    }

    /// <summary>
    /// Error monad.
    /// </summary>
    internal struct Error
    {
        public static Error OK = new(true, null);

        private readonly bool _success;
        private readonly Exception? _exception;

        public Error(bool success, Exception? exception)
        {
            _success = success;
            _exception = success ? null : exception;
        }

        public readonly Error Success(Action action)
        {
            if (_success)
            {
                action();
            }

            return this;
        }

        /// <summary>
        /// Takes a lambda what to do if the result failed.
        /// Returns the result so that it can be managed in whatever way is needed.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public readonly Error Exception(Action<Exception?> action)
        {
            if (!_success)
            {
                action(_exception);
            }

            return this;
        }
    }

    /// <summary>
    /// Error monad.
    /// </summary>
    /// <typeparam name="T">Encapsulated success-action parameter type</typeparam>
    internal readonly struct Error<T>
    {
        private readonly bool _success;
        private readonly Exception? _exception;
        private readonly T _parameter;

        public Error(bool success, Exception? exception, T parameter)
        {
            _success = success;
            _exception = success ? null : exception;
            _parameter = parameter;
        }

        /// <summary>
        /// Takes a lambda what to do if the result failed.
        /// Returns the result so that it can be managed in whatever way is needed.
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public Error<T> Exception(Action<Exception?> action)
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
