using System.Globalization;
using System.Text;

namespace TrKarekod.Tlv;

/// <summary>Appends data objects in ID-length-value form, counting the length in characters.</summary>
public sealed class TlvWriter
{
    private readonly StringBuilder _builder = new(256);

    /// <summary>Number of UTF-16 code units written so far.</summary>
    public int Length => _builder.Length;

    /// <summary>Appends a primitive object.</summary>
    /// <exception cref="ArgumentException">The ID is not two digits or the value is empty or longer than 99 characters.</exception>
    public TlvWriter Write(string id, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (id is not { Length: 2 } || !char.IsAsciiDigit(id[0]) || !char.IsAsciiDigit(id[1]))
        {
            throw new ArgumentException($"ID must be two decimal digits, was \"{id}\".", nameof(id));
        }

        var length = RuneText.Length(value);
        if (length is < 1 or > 99)
        {
            throw new ArgumentException($"Value of {id} must be 1-99 characters, was {length}.", nameof(value));
        }

        _builder.Append(id).Append(length.ToString("D2", CultureInfo.InvariantCulture)).Append(value);
        return this;
    }

    /// <summary>Appends a template whose content is produced by <paramref name="content"/>; skipped when empty.</summary>
    public TlvWriter WriteTemplate(string id, Action<TlvWriter> content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var inner = new TlvWriter();
        content(inner);
        if (inner.Length > 0)
        {
            Write(id, inner.ToString());
        }

        return this;
    }

    /// <summary>
    /// Appends the CRC object: the tag and length "6304" are part of the checksummed data, the value is not.
    /// </summary>
    public string WriteCrcAndFinish()
    {
        _builder.Append("6304");
        var crc = Crc16Ccitt.ComputeUtf8(_builder.ToString());
        return _builder.Append(Crc16Ccitt.ToHex(crc)).ToString();
    }

    /// <inheritdoc />
    public override string ToString() => _builder.ToString();
}
