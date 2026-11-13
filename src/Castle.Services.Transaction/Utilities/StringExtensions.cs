#region License
// Copyright (c) 2004-2026 Castle Project - https://www.castleproject.org/
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

using System.Diagnostics.CodeAnalysis;
#if !NET11_0_OR_GREATER || NETFRAMEWORK
using System.Globalization;
#endif
using System.Runtime.CompilerServices;
#if !NET11_0_OR_GREATER && !NETFRAMEWORK
using System.Runtime.InteropServices;
#endif

namespace Castle.Services.Transaction.Utilities
{
    public static class StringExtensions
    {
        /// <summary>
        ///     Indicates whether this <see cref="string" /> instance is <see langword="null" />, an empty string (""), or consists only of white-space characters.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <returns>
        ///     <see langword="true" /> if the input string is <see langword="null" />, an empty string (""), or consists only of white-space characters;
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <remarks>
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.isnullorwhitespace" />
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/nullable-analysis" />
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? str)
        {
            return string.IsNullOrWhiteSpace(str);
        }

        /// <summary>
        ///     Indicates whether the specified string is <see langword="null" /> or an empty string ("").
        /// </summary>
        /// <param name="str">The string to test.</param>
        /// <returns>
        ///     <see langword="true" /> if the value parameter is <see langword="null" /> or an empty string ("");
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <remarks>
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.isnullorempty" />
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/attributes/nullable-analysis" />
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNullOrEmpty([NotNullWhen(false)] this string? str)
        {
            return string.IsNullOrEmpty(str);
        }

#if NETFRAMEWORK
        /// <summary>
        ///     Indicates whether the specified <see cref="ReadOnlySpan{T}" /> instance consists only of white-space characters.
        /// </summary>
        /// <param name="span">The input character span.</param>
        /// <returns>
        ///     <see langword="true" /> if the character span consists only of white-space characters;
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <remarks>
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.memoryextensions.iswhitespace" />
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.isnullorwhitespace" />
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWhiteSpace(this ReadOnlySpan<char> span)
        {
            // Unroll loop: process 4 characters at a time to reduce branching overhead.
            while (span.Length >= 4)
            {
                if (!char.IsWhiteSpace(span[0]) ||
                    !char.IsWhiteSpace(span[1]) ||
                    !char.IsWhiteSpace(span[2]) ||
                    !char.IsWhiteSpace(span[3]))
                {
                    return false;
                }

                span = span[4..];
            }

            // Process remaining 0 to 3 characters.
            for (var i = 0; i < span.Length; i++)
            {
                if (!char.IsWhiteSpace(span[i]))
                {
                    return false;
                }
            }

            return true;
        }

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
            this string? str,
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
            this string? str,
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
            this string? str,
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

#if !NET11_0_OR_GREATER
        /// <summary>
        ///     Reports the zero-based index of the last occurrence of the specified Unicode character in this <see cref="string" /> instance.
        ///     A parameter specifies the type of search to use for the specified character.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <param name="value">The character to seek.</param>
        /// <param name="comparisonType">
        ///     One of the enumeration values that specifies the rules to use in the comparison.
        /// </param>
        /// <returns>
        ///     The zero-based index of the last occurrence of <c>value</c> if found;
        ///     otherwise, -1.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="str" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.lastindexof" />
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int LastIndexOf(
            this string? str,
            char value,
            StringComparison comparisonType)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(str);
#else
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }
#endif

            return comparisonType switch
            {
                StringComparison.Ordinal => str.LastIndexOf(value),
#if NET
                StringComparison.OrdinalIgnoreCase => str.AsSpan().LastIndexOf(MemoryMarshal.CreateReadOnlySpan(ref value, 1), StringComparison.OrdinalIgnoreCase),
#else
                StringComparison.OrdinalIgnoreCase => CultureInfo.InvariantCulture.CompareInfo.LastIndexOf(str, value, CompareOptions.OrdinalIgnoreCase),
#endif
                StringComparison.CurrentCulture => CultureInfo.CurrentCulture.CompareInfo.LastIndexOf(str, value, CompareOptions.None),
                StringComparison.CurrentCultureIgnoreCase => CultureInfo.CurrentCulture.CompareInfo.LastIndexOf(str, value, CompareOptions.IgnoreCase),
                StringComparison.InvariantCulture => CultureInfo.InvariantCulture.CompareInfo.LastIndexOf(str, value, CompareOptions.None),
                StringComparison.InvariantCultureIgnoreCase => CultureInfo.InvariantCulture.CompareInfo.LastIndexOf(str, value, CompareOptions.IgnoreCase),
                _ => throw new ArgumentOutOfRangeException(nameof(comparisonType), comparisonType, null)
            };
        }
#endif

#if NETFRAMEWORK
        /// <summary>
        ///     Determines whether the <see cref="string" /> instance starts with the specified character.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <param name="value">The character to compare.</param>
        /// <returns>
        ///     <see langword="true" /> if <c>value</c> matches the beginning of this instance;
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="str" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     This method performs an ordinal (case-sensitive and culture-insensitive) comparison.
        ///
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.startswith" />
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool StartsWith(this string? str, char value)
        {
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            return str.Length > 0 && str[0] == value;
        }

        /// <summary>
        ///     Determines whether the end of this <see cref="string" /> instance matches the specified character.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <param name="value">The character to compare to the character at the end of this instance.</param>
        /// <returns>
        ///     <see langword="true" /> if <c>value</c> matches the end of this instance;
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="str" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     This method performs an ordinal (case-sensitive and culture-insensitive) comparison.
        ///
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.endswith" />
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EndsWith(this string? str, char value)
        {
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }

            return str.Length > 0 && str[^1] == value;
        }
#endif

#if !NET11_0_OR_GREATER
        /// <summary>
        ///     Determines whether the end of this <see cref="string" /> instance matches the specified character.
        /// </summary>
        /// <param name="str">The input string.</param>
        /// <param name="value">The character to compare to the character at the end of this instance.</param>
        /// <param name="comparisonType">
        ///     One of the enumeration values that specifies the rules to use in the comparison.
        /// </param>
        /// <returns>
        ///     <see langword="true" /> if <c>value</c> matches the end of this instance;
        ///     otherwise, <see langword="false" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">If <paramref name="str" /> is <see langword="null" />.</exception>
        /// <remarks>
        ///     REFERENCES:
        ///     -   <see href="https://learn.microsoft.com/en-us/dotnet/api/system.string.endswith" />
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool EndsWith(
            this string? str,
            char value,
            StringComparison comparisonType)
        {
#if NET8_0_OR_GREATER
            ArgumentNullException.ThrowIfNull(str);
#else
            if (str is null)
            {
                throw new ArgumentNullException(nameof(str));
            }
#endif

#if NET
            return str.AsSpan().EndsWith(MemoryMarshal.CreateReadOnlySpan(ref value, 1), comparisonType);
#else
            ReadOnlySpan<char> target = stackalloc char[] { value };
            return str.AsSpan().EndsWith(target, comparisonType);
#endif
        }
#endif
    }
}
