using System.Text;
using Operant.SaveFormat.Internal;
using Xunit;

namespace Operant.SaveFormat.Tests;

public class SpookyHashV2Tests
{
    // Vectors generated from the reference Python `spookyhash` library, which
    // tracks the canonical C source. Includes both short-path (<192 bytes) and
    // long-path (>=192 bytes) inputs.
    [Theory]
    [InlineData("",                  0UL, 0UL, 0x232706fc6bf50919UL, 0x8b72ee65b4e851c7UL)]
    [InlineData("a",                 0UL, 0UL, 0x1a108191a0bbc9bdUL, 0x754258f061412a92UL)]
    [InlineData("abc",               0UL, 0UL, 0x8aab15f77537c967UL, 0xc61367f8ca7811b0UL)]
    [InlineData("C4SaveFileHeader",  0UL, 0UL, 0x4014318e40b2bd37UL, 0x98954c5d147b411fUL)]
    public void ShortPath_KnownAsciiVectors(string ascii, ulong s1, ulong s2, ulong eh1, ulong eh2)
    {
        var (h1, h2) = SpookyHashV2.Hash128(Encoding.ASCII.GetBytes(ascii), s1, s2);
        Assert.Equal(eh1, h1);
        Assert.Equal(eh2, h2);
    }

    [Fact]
    public void LongPath_192Bytes()
    {
        byte[] input = new byte[192];
        for (int i = 0; i < input.Length; i++) input[i] = (byte)(i & 0xFF);
        var (h1, h2) = SpookyHashV2.Hash128(input, 0, 0);
        Assert.Equal(0x02d13f94b2a31a54UL, h1);
        Assert.Equal(0x0ea393db758d85d3UL, h2);
    }

    [Fact]
    public void LongPath_LargeInputWithSeeds()
    {
        byte[] input = new byte[11824];
        for (int i = 0; i < input.Length; i++) input[i] = (byte)(((i * 7) + 3) & 0xFF);
        var (h1, h2) = SpookyHashV2.Hash128(input, 0xdeadbeefUL, 0x12345678UL);
        Assert.Equal(0x15c995dd5337da37UL, h1);
        Assert.Equal(0x0fb7c2588aee1a6dUL, h2);
    }
}
