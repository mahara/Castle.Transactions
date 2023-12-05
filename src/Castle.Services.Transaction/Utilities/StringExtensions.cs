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

namespace Castle.Services.Transaction.Utilities
{
    public static class StringExtensions
    {
#if NETFRAMEWORK
        /// <summary>
        ///     Returns a value indicating whether the specified <see cref="string" /> value occurs within this <see cref="string" /> instance,
        ///     when compared using the specified comparison type.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <param name="value">The string value to seek.</param>
        /// <param name="comparisonType">
        ///     One of the enumeration values that determines how these strings are compared.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the <c>value</c> parameter occurs within this <see cref="string" /> instance, or if <c>value</c> is the empty string ("");
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="str" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.contains" />
        /// </remarks>
        public static bool Contains(
            this string str,
            string value,
            StringComparison comparisonType)
        {
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return str.IndexOf(value, comparisonType) >= 0;
        }

        /// <summary>
        ///     Returns a value indicating whether the specified <see cref="char" /> value occurs within this <see cref="string" /> instance,
        ///     when compared using the specified comparison type.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <param name="value">The character to seek.</param>
        /// <param name="comparisonType">
        ///     One of the enumeration values that determines how these strings are compared.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the <c>value</c> parameter occurs within this <see cref="string" /> instance;
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="str" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.contains" />
        /// </remarks>
        public static bool Contains(
            this string str,
            char value,
            StringComparison comparisonType)
        {
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            return str.IndexOf(value.ToString(), comparisonType) >= 0;
        }

        /// <summary>
        ///     Determines whether the end of this <see cref="string" /> instance matches the specified character.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <param name="value">The character to compare to the character at the end of this instance.</param>
        /// <returns><see langword="true" /> if <c>value</c> matches the end of this instance; otherwise, <see langword="false" />.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="str" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     This method performs an ordinal (case-sensitive and culture-insensitive) comparison.
        ///
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.endswith" />
        /// </remarks>
        public static bool EndsWith(this string str, char value)
        {
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            return str.EndsWith(value.ToString(), StringComparison.Ordinal);
        }
#endif
    }
}
