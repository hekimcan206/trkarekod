namespace TrKarekod.Tlv;

/// <summary>
/// Length arithmetic in characters (Unicode scalar values), which is how EMVCo and TR Karekod count the two-digit
/// length. Turkish letters are single UTF-16 code units, so the fast path is the common one; surrogate pairs are
/// counted as one character.
/// </summary>
internal static class RuneText
{
    /// <summary>Number of characters in <paramref name="text"/>; a lone surrogate counts as one.</summary>
    public static int Length(ReadOnlySpan<char> text)
    {
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
            {
                i++;
            }

            count++;
        }

        return count;
    }

    /// <summary>
    /// Advances <paramref name="characters"/> characters from <paramref name="start"/>; returns the UTF-16 index after
    /// them, or -1 when the text ends first.
    /// </summary>
    public static int Advance(ReadOnlySpan<char> text, int start, int characters)
    {
        var i = start;
        for (var n = 0; n < characters; n++)
        {
            if (i >= text.Length)
            {
                return -1;
            }

            i += char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
        }

        return i;
    }

    /// <summary>True when the text contains an unpaired surrogate (it would not survive UTF-8 encoding).</summary>
    public static bool HasLoneSurrogate(ReadOnlySpan<char> text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (char.IsHighSurrogate(text[i]))
            {
                if (i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                {
                    return true;
                }

                i++;
            }
            else if (char.IsLowSurrogate(text[i]))
            {
                return true;
            }
        }

        return false;
    }
}
