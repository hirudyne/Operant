using System.Buffers.Binary;
using Operant.SaveFormat.Internal;

namespace Operant.SaveFormat;

/// <summary>
/// Reproduces Unity's <c>Hash128</c> incremental hasher used by the Zero
/// Parades save format. State is two 64-bit values, initially zero; each
/// <c>Append</c> mixes the new bytes in via SpookyHash V2 128-bit using the
/// current state as the seed pair.
///
/// String inputs are hashed as UTF-8 bytes - not UTF-16 - even though C#
/// strings are stored as UTF-16 internally. This is the empirically-verified
/// behaviour of the game's <c>Hash128.Append(string)</c> overload.
/// </summary>
public struct Hash128
{
    private ulong _h0;
    private ulong _h1;

    public Hash128() { _h0 = 0UL; _h1 = 0UL; }

    public void Append(ReadOnlySpan<byte> data)
    {
        (_h0, _h1) = SpookyHashV2.Hash128(data, _h0, _h1);
    }

    public void Append(string s)
    {
        int byteCount = System.Text.Encoding.UTF8.GetByteCount(s);
        if (byteCount <= 256)
        {
            Span<byte> buf = stackalloc byte[byteCount];
            System.Text.Encoding.UTF8.GetBytes(s, buf);
            Append(buf);
        }
        else
        {
            byte[] buf = System.Text.Encoding.UTF8.GetBytes(s);
            Append(buf);
        }
    }

    public void Append(int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        Append(buf);
    }

    /// <summary>
    /// Renders the current state as 32 lowercase hex chars:
    /// <c>h0.ToBytes(le) || h1.ToBytes(le)</c>, both as little-endian.
    /// </summary>
    public override string ToString()
    {
        Span<byte> buf = stackalloc byte[16];
        BinaryPrimitives.WriteUInt64LittleEndian(buf, _h0);
        BinaryPrimitives.WriteUInt64LittleEndian(buf[8..], _h1);
        return Convert.ToHexString(buf).ToLowerInvariant();
    }

    /// <summary>
    /// Convenience: compute a single-chunk checksum as the game does for
    /// SaveFileHeader / SaveFileContent.
    /// </summary>
    public static string Compute(string contentTag, int version, ReadOnlySpan<byte> dataBlob)
    {
        var h = new Hash128();
        h.Append(contentTag);
        h.Append(version);
        h.Append(dataBlob);
        return h.ToString();
    }
}
