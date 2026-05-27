// Port of Bob Jenkins' SpookyHash V2 (public domain), 128-bit Hash() entry point.
//
// Reference: http://burtleburtle.net/bob/hash/spooky.html
// Verified to match Unity's Hash128.Append(...) against existing Zero Parades
// saves. The short path handles inputs < 192 bytes; the long path handles
// the rest. Both are exercised by the save format (e.g. the encrypted content
// blob is ~12 KiB).

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace Operant.SaveFormat.Internal;

internal static class SpookyHashV2
{
    private const ulong ScConst = 0xDEADBEEFDEADBEEFUL;
    private const int ScNumVars = 12;
    private const int ScBlockSize = ScNumVars * 8;       // 96 bytes
    private const int ScBufSize   = 2 * ScBlockSize;     // 192 bytes

    /// <summary>
    /// Computes SpookyHash V2 128-bit of <paramref name="message"/> with the
    /// given seed pair, returning (hash1, hash2).
    /// </summary>
    public static (ulong h1, ulong h2) Hash128(ReadOnlySpan<byte> message, ulong seed1, ulong seed2)
    {
        return message.Length < ScBufSize
            ? ShortHash(message, seed1, seed2)
            : LongHash(message, seed1, seed2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Rot64(ulong x, int k) => (x << k) | (x >> (64 - k));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong ReadU64(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadUInt64LittleEndian(data[offset..]);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ReadU32(ReadOnlySpan<byte> data, int offset)
        => BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);

    // --- Short variant ----------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ShortMix(ref ulong h0, ref ulong h1, ref ulong h2, ref ulong h3)
    {
        h2 = Rot64(h2, 50); h2 += h3; h0 ^= h2;
        h3 = Rot64(h3, 52); h3 += h0; h1 ^= h3;
        h0 = Rot64(h0, 30); h0 += h1; h2 ^= h0;
        h1 = Rot64(h1, 41); h1 += h2; h3 ^= h1;
        h2 = Rot64(h2, 54); h2 += h3; h0 ^= h2;
        h3 = Rot64(h3, 48); h3 += h0; h1 ^= h3;
        h0 = Rot64(h0, 38); h0 += h1; h2 ^= h0;
        h1 = Rot64(h1, 37); h1 += h2; h3 ^= h1;
        h2 = Rot64(h2, 62); h2 += h3; h0 ^= h2;
        h3 = Rot64(h3, 34); h3 += h0; h1 ^= h3;
        h0 = Rot64(h0,  5); h0 += h1; h2 ^= h0;
        h1 = Rot64(h1, 36); h1 += h2; h3 ^= h1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ShortEnd(ref ulong h0, ref ulong h1, ref ulong h2, ref ulong h3)
    {
        h3 ^= h2; h2 = Rot64(h2, 15); h3 += h2;
        h0 ^= h3; h3 = Rot64(h3, 52); h0 += h3;
        h1 ^= h0; h0 = Rot64(h0, 26); h1 += h0;
        h2 ^= h1; h1 = Rot64(h1, 51); h2 += h1;
        h3 ^= h2; h2 = Rot64(h2, 28); h3 += h2;
        h0 ^= h3; h3 = Rot64(h3,  9); h0 += h3;
        h1 ^= h0; h0 = Rot64(h0, 47); h1 += h0;
        h2 ^= h1; h1 = Rot64(h1, 54); h2 += h1;
        h3 ^= h2; h2 = Rot64(h2, 32); h3 += h2;
        h0 ^= h3; h3 = Rot64(h3, 25); h0 += h3;
        h1 ^= h0; h0 = Rot64(h0, 63); h1 += h0;
    }

    private static (ulong, ulong) ShortHash(ReadOnlySpan<byte> message, ulong hash1, ulong hash2)
    {
        ulong a = hash1, b = hash2, c = ScConst, d = ScConst;
        int length = message.Length;
        int pos = 0;

        if (length > 15)
        {
            int end = length - (length & 31);
            while (pos < end)
            {
                c += ReadU64(message, pos);
                d += ReadU64(message, pos + 8);
                ShortMix(ref a, ref b, ref c, ref d);
                a += ReadU64(message, pos + 16);
                b += ReadU64(message, pos + 24);
                pos += 32;
            }
        }
        int remainder = length - pos;
        if (remainder >= 16)
        {
            c += ReadU64(message, pos);
            d += ReadU64(message, pos + 8);
            ShortMix(ref a, ref b, ref c, ref d);
            pos += 16;
            remainder -= 16;
        }

        d += ((ulong)length) << 56;
        ReadOnlySpan<byte> tail = message[pos..];
        switch (remainder)
        {
            case 15: d += ((ulong)tail[14]) << 48; goto case 14;
            case 14: d += ((ulong)tail[13]) << 40; goto case 13;
            case 13: d += ((ulong)tail[12]) << 32; goto case 12;
            case 12: d += ReadU32(tail, 8); c += ReadU64(tail, 0); break;
            case 11: d += ((ulong)tail[10]) << 16; goto case 10;
            case 10: d += ((ulong)tail[9]) << 8; goto case 9;
            case 9:  d += tail[8]; c += ReadU64(tail, 0); break;
            case 8:  c += ReadU64(tail, 0); break;
            case 7:  c += ((ulong)tail[6]) << 48; goto case 6;
            case 6:  c += ((ulong)tail[5]) << 40; goto case 5;
            case 5:  c += ((ulong)tail[4]) << 32; goto case 4;
            case 4:  c += ReadU32(tail, 0); break;
            case 3:  c += ((ulong)tail[2]) << 16; goto case 2;
            case 2:  c += ((ulong)tail[1]) << 8; goto case 1;
            case 1:  c += tail[0]; break;
            case 0:  c += ScConst; d += ScConst; break;
        }
        ShortEnd(ref a, ref b, ref c, ref d);
        return (a, b);
    }

    // --- Long variant -----------------------------------------------------

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Mix(ReadOnlySpan<byte> data, int pos, ulong[] s)
    {
        s[0]  += ReadU64(data, pos +  0); s[2]  ^= s[10]; s[11] ^= s[0];  s[0]  = Rot64(s[0], 11);  s[11] += s[1];
        s[1]  += ReadU64(data, pos +  8); s[3]  ^= s[11]; s[0]  ^= s[1];  s[1]  = Rot64(s[1], 32);  s[0]  += s[2];
        s[2]  += ReadU64(data, pos + 16); s[4]  ^= s[0];  s[1]  ^= s[2];  s[2]  = Rot64(s[2], 43);  s[1]  += s[3];
        s[3]  += ReadU64(data, pos + 24); s[5]  ^= s[1];  s[2]  ^= s[3];  s[3]  = Rot64(s[3], 31);  s[2]  += s[4];
        s[4]  += ReadU64(data, pos + 32); s[6]  ^= s[2];  s[3]  ^= s[4];  s[4]  = Rot64(s[4], 17);  s[3]  += s[5];
        s[5]  += ReadU64(data, pos + 40); s[7]  ^= s[3];  s[4]  ^= s[5];  s[5]  = Rot64(s[5], 28);  s[4]  += s[6];
        s[6]  += ReadU64(data, pos + 48); s[8]  ^= s[4];  s[5]  ^= s[6];  s[6]  = Rot64(s[6], 39);  s[5]  += s[7];
        s[7]  += ReadU64(data, pos + 56); s[9]  ^= s[5];  s[6]  ^= s[7];  s[7]  = Rot64(s[7], 57);  s[6]  += s[8];
        s[8]  += ReadU64(data, pos + 64); s[10] ^= s[6];  s[7]  ^= s[8];  s[8]  = Rot64(s[8], 55);  s[7]  += s[9];
        s[9]  += ReadU64(data, pos + 72); s[11] ^= s[7];  s[8]  ^= s[9];  s[9]  = Rot64(s[9], 54);  s[8]  += s[10];
        s[10] += ReadU64(data, pos + 80); s[0]  ^= s[8];  s[9]  ^= s[10]; s[10] = Rot64(s[10], 22); s[9]  += s[11];
        s[11] += ReadU64(data, pos + 88); s[1]  ^= s[9];  s[10] ^= s[11]; s[11] = Rot64(s[11], 46); s[10] += s[0];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void EndPartial(ulong[] h)
    {
        h[11] += h[1];  h[2]  ^= h[11]; h[1]  = Rot64(h[1], 44);
        h[0]  += h[2];  h[3]  ^= h[0];  h[2]  = Rot64(h[2], 15);
        h[1]  += h[3];  h[4]  ^= h[1];  h[3]  = Rot64(h[3], 34);
        h[2]  += h[4];  h[5]  ^= h[2];  h[4]  = Rot64(h[4], 21);
        h[3]  += h[5];  h[6]  ^= h[3];  h[5]  = Rot64(h[5], 38);
        h[4]  += h[6];  h[7]  ^= h[4];  h[6]  = Rot64(h[6], 33);
        h[5]  += h[7];  h[8]  ^= h[5];  h[7]  = Rot64(h[7], 10);
        h[6]  += h[8];  h[9]  ^= h[6];  h[8]  = Rot64(h[8], 13);
        h[7]  += h[9];  h[10] ^= h[7];  h[9]  = Rot64(h[9], 38);
        h[8]  += h[10]; h[11] ^= h[8];  h[10] = Rot64(h[10], 53);
        h[9]  += h[11]; h[0]  ^= h[9];  h[11] = Rot64(h[11], 42);
        h[10] += h[0];  h[1]  ^= h[10]; h[0]  = Rot64(h[0], 54);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void End(ReadOnlySpan<byte> data, int pos, ulong[] h)
    {
        for (int i = 0; i < ScNumVars; i++)
            h[i] += ReadU64(data, pos + i * 8);
        EndPartial(h);
        EndPartial(h);
        EndPartial(h);
    }

    private static (ulong, ulong) LongHash(ReadOnlySpan<byte> message, ulong hash1, ulong hash2)
    {
        int length = message.Length;
        ulong[] h = new ulong[ScNumVars];
        h[0] = h[3] = h[6] = h[9]  = hash1;
        h[1] = h[4] = h[7] = h[10] = hash2;
        h[2] = h[5] = h[8] = h[11] = ScConst;

        int pos = 0;
        int end = (length / ScBlockSize) * ScBlockSize;
        while (pos < end)
        {
            Mix(message, pos, h);
            pos += ScBlockSize;
        }

        // Handle the last whole or partial block. Reference implementation pads
        // the tail with zeros up to a full block, then sets the high byte to
        // the remainder length.
        int remainder = length - pos;
        Span<byte> buf = stackalloc byte[ScBlockSize];
        message[pos..].CopyTo(buf);
        buf[ScBlockSize - 1] = (byte)remainder;

        End(buf, 0, h);
        return (h[0], h[1]);
    }
}
