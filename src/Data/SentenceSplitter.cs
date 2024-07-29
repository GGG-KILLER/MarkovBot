using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace MarkovBot.Data;

public static class SentenceSplitter
{
    private static readonly SearchValues<string> s_linkStarts = SearchValues.Create(["http://", "https://"], StringComparison.Ordinal);

    public static IEnumerable<string> SplitIntoParts(string sentence)
    {
        // TODO: Use ICU Boundary Analysis? How to figure out locale?

        sentence = sentence.Normalize(NormalizationForm.FormC);

        var linkIndex = 0;
        var linksBuilder = new List<uint>();
        while ((linkIndex = sentence.AsSpan(linkIndex).IndexOfAny(s_linkStarts)) != -1)
        {
            linksBuilder.Add((uint)linkIndex);

            // Links last until the last space for simplicity sake.
            var end = sentence.Skip(linkIndex)
                              .Index()
                              .FirstOrDefault(tuple => char.IsWhiteSpace(tuple.Item))
                              .Index;
            if (end == 0) end = sentence.Length;
            linksBuilder.Add((uint)end);

            // Skip to the end of the link
            linkIndex = end;
        }

        var start = 0;
        var position = 0;
        foreach (var rune in sentence.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                // Return the word until now.
                if (start != position)
                    yield return sentence[start..position];

                // Start on the next char since we're past the whitespace
                start = position + rune.Utf16SequenceLength;
            }
            else if (Rune.IsPunctuation(rune)
                && rune.Value is not '\'' // We ignore ' because of its use in english
                && !IsInRanges(CollectionsMarshal.AsSpan(linksBuilder), (uint)position))
            {
                // Return the word until now.
                if (start != position)
                    yield return sentence[start..position];
                // Return the punctuation itself
                yield return rune.ToString();

                // Start on the next char since we're past the punctuation
                start = position + rune.Utf16SequenceLength;
            }

            // TODO: Handle emojis

            position += rune.Utf16SequenceLength;
        }

        if (start != position)
            yield return sentence[start..position];
    }

    /// <summary>
    /// Checks whether the provided <paramref name="value" /> is in the range [<paramref
    /// name="start" />, <paramref name="end" />].
    /// </summary>
    /// <param name="start">The first index of the range (inclusive).</param>
    /// <param name="value">The index to check for.</param>
    /// <param name="end">The last index of the range (inclusive).</param>
    /// <returns>Whether the provided index is in the range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsInRange(uint start, uint value, uint end) =>
        (ulong)(value - start) <= (end - start);

    /// <summary>
    /// Checks if the provided index is in the middle of any of the ranges
    /// in the provided SORTED AND FLATTENED range list.
    /// </summary>
    /// <param name="ranges">The sorted and flattened list.</param>
    /// <param name="ch">The index to find.</param>
    /// <returns></returns>
    private static bool IsInRanges(ReadOnlySpan<uint> ranges, uint ch) =>
        ranges.Length == 2
        ? IsInRange(ranges[0], ch, ranges[1])
        : InnerIsInRangesIndexCheck(ranges.BinarySearch(ch));

    /// <summary>
    /// Checks if the provided character is in the middle of any of the ranges
    /// in the provided (sorted and flattened) list.
    /// </summary>
    /// <param name="idx">The index found by binary search.</param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool InnerIsInRangesIndexCheck(int idx) =>
        // If the next greatest value's index is odd, then the character is in
        // the middle of a range. Since the length is always even, we don't need
        // to worry about the element not being in the array since it'll return 0
        // or an even number which will not pass the odd check.
        idx >= 0 || (idx & 1) == 0;
}