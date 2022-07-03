#region License
// Copyright (c) 2004-2022 Castle Project - https://www.castleproject.org/
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
using System.Globalization;
using System.Runtime.CompilerServices;
#endif

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
        ///     One of the enumeration values that specifies the rules to use in the comparison.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if the <c>value</c> parameter occurs within this <see cref="string" /> instance, or if <c>value</c> is the empty string ("");
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="str" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentNullException">If <paramref name="value" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.contains" />
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        ///     One of the enumeration values that specifies the rules to use in the comparison.
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(
            this string str,
            char value,
            StringComparison comparisonType)
        {
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            return str.IndexOf(value, comparisonType) >= 0;
        }

        /// <summary>
        ///     Reports the zero-based index of the first occurrence of the specified Unicode character in this <see cref="string" /> instance.
        ///     A parameter specifies the type of search to use for the specified character.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <param name="value">The character to seek.</param>
        /// <param name="comparisonType">
        ///     One of the enumeration values that specifies the rules to use in the comparison.
        /// </param>
        /// <returns>
        ///     The zero-based index of the first occurrence of <c>value</c> if found;
        ///     otherwise, -1.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="str" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.indexof" />
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int IndexOf(
            this string str,
            char value,
            StringComparison comparisonType)
        {
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            return comparisonType switch
            {
                StringComparison.Ordinal => str.IndexOf(value),
                StringComparison.OrdinalIgnoreCase => CultureInfo.InvariantCulture.CompareInfo.IndexOf(str, value, CompareOptions.OrdinalIgnoreCase),
                StringComparison.CurrentCulture => CultureInfo.CurrentCulture.CompareInfo.IndexOf(str, value, CompareOptions.None),
                StringComparison.CurrentCultureIgnoreCase => CultureInfo.CurrentCulture.CompareInfo.IndexOf(str, value, CompareOptions.IgnoreCase),
                StringComparison.InvariantCulture => CultureInfo.InvariantCulture.CompareInfo.IndexOf(str, value, CompareOptions.None),
                StringComparison.InvariantCultureIgnoreCase => CultureInfo.InvariantCulture.CompareInfo.IndexOf(str, value, CompareOptions.IgnoreCase),
                _ => throw new ArgumentOutOfRangeException(nameof(comparisonType), comparisonType, null)
            };
        }
#endif
    }
}
