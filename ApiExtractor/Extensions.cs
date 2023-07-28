using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security;
using System.Text.RegularExpressions;

namespace ApiExtractor;

public static class Extensions {

    public static string? EmptyToNull(this string? original) => string.IsNullOrEmpty(original) ? null : original;

    [return: NotNullIfNotNull(nameof(stringWithNewLines))]
    public static string? NewLinesToParagraphs(this string? stringWithNewLines, bool excludeOuterTags = false) =>
        string.IsNullOrEmpty(stringWithNewLines)
            ? stringWithNewLines
            : (excludeOuterTags ? "" : "<para>") + string.Join("</para><para>", Regex.Split(stringWithNewLines, @"\r?\n").Select(SecurityElement.Escape)) + (excludeOuterTags ? "" : "</para>");

    [return: NotNullIfNotNull(nameof(input))]
    public static string? ToLowerFirstLetter(this string? input) => string.IsNullOrEmpty(input) ? input : char.ToLowerInvariant(input[0]) + input[1..];

    [return: NotNullIfNotNull(nameof(input))]
    public static string? ToUpperFirstLetter(this string? input) => string.IsNullOrEmpty(input) ? input : char.ToUpperInvariant(input[0]) + input[1..];

    public static IEnumerable<T> DistinctConsecutive<T>(this IEnumerable<T> original, IEqualityComparer<T>? comparer = null) {
        comparer ??= EqualityComparer<T>.Default;
        T?   previousItem = default;
        bool isFirstItem  = true;

        foreach (T item in original) {
            if (isFirstItem || !comparer.Equals(previousItem, item)) {
                yield return item;
            }

            previousItem = item;
            isFirstItem  = false;
        }
    }

}