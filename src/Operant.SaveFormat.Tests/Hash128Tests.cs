using System.Text;
using Xunit;

namespace Operant.SaveFormat.Tests;

public class Hash128Tests
{
    [Fact]
    public void Empty_Hash_MatchesSpookyV2InitialMix()
    {
        var h = new Hash128();
        // The initial state (0,0) hashed with no Append calls renders as
        // 0000...0000 since ToString just serialises the state, but the moment
        // anything is appended the state changes deterministically.
        Assert.Equal("00000000000000000000000000000000", h.ToString());
    }

    [Fact]
    public void Compute_HeaderChunk_KnownVector()
    {
        // From the small save: empty header version=1, content tag, hashing
        // the bare header tag with version=1 and an empty data blob gives a
        // deterministic value we can pin.
        string h = Hash128.Compute("C4SaveFileHeader", 1, Array.Empty<byte>());
        // Computed via the Python reference for the same inputs.
        Assert.Equal("675d2cb4efa0939083bf83aa8b319017", h);
    }

    [Fact]
    public void StringAppend_UsesUtf8Encoding()
    {
        // "café" UTF-8 is 5 bytes, UTF-16-LE is 8 bytes. If we accidentally
        // hash as UTF-16 we'd get a different result.
        var hUtf8 = new Hash128();
        hUtf8.Append("café");
        var hRaw = new Hash128();
        hRaw.Append(Encoding.UTF8.GetBytes("café"));
        Assert.Equal(hUtf8.ToString(), hRaw.ToString());

        var hUtf16 = new Hash128();
        hUtf16.Append(Encoding.Unicode.GetBytes("café"));
        Assert.NotEqual(hUtf8.ToString(), hUtf16.ToString());
    }
}
