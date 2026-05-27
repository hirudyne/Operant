using System.Text.Json.Nodes;
using Xunit;

namespace Operant.SaveFormat.Tests;

public class SaveFileTests
{
    private const string SmallSavePath = "TestData/6c767ac5-42c1-4071-bb62-b8c559926c2c.sav";
    private const string BigSavePath = "TestData/0242cac8-d7f9-4a0d-9fbc-cc01765aad2f.sav";

    // Hashes captured from the known-good original files. If checksums change,
    // either the test data was modified or the hash implementation drifted.
    private const string SmallHeaderHash  = "1cfef8eb03ff58be87828e6f05104121";
    private const string SmallContentHash = "52258e4d7057c198a5c4289cf2e74b18";
    private const string BigHeaderHash    = "9da1a419ae9393b7e10ad3a7b0e414e2";
    private const string BigContentHash   = "f5d857b8398f70d0f248286b2decb9de";

    [Fact]
    public void Read_SmallSave_LoadsHeaderAndContent()
    {
        var save = SaveFile.Read(SmallSavePath);
        Assert.Equal(26, save.Sol);
        Assert.Equal("PRA", (string?)save.Content["SceneName"]);
        Assert.True(save.EncryptionRequired);
    }

    [Fact]
    public void Read_BigSave_LoadsInventoryAndCounters()
    {
        var save = SaveFile.Read(BigSavePath);
        Assert.Equal(13, save.Sol);
        Assert.Equal(32, save.Inventory.Count);
        Assert.True(save.TryGetCounter("stats.money", out int money));
        Assert.Equal(13, money);
        Assert.True(save.TryGetCounter("skills.muscle", out int muscle));
        Assert.Equal(2, muscle);
    }

    [Fact]
    public void Hash128_MatchesFileHashes_Small()
    {
        // Reading the file again to extract the raw blobs so we can validate
        // the recomputed hashes against the file's own stored hashes.
        AssertHashesMatch(SmallSavePath, SmallHeaderHash, SmallContentHash);
    }

    [Fact]
    public void Hash128_MatchesFileHashes_Big()
    {
        AssertHashesMatch(BigSavePath, BigHeaderHash, BigContentHash);
    }

    [Fact]
    public void RoundTrip_SmallSave_PreservesAllFields()
    {
        var original = SaveFile.Read(SmallSavePath);
        byte[] serialized = original.Serialize();
        var reread = SaveFile.Parse(serialized);

        Assert.Equal(original.Sol, reread.Sol);
        Assert.Equal(original.Header.ToJsonString(), reread.Header.ToJsonString());
        Assert.Equal(original.Content.ToJsonString(), reread.Content.ToJsonString());
    }

    [Fact]
    public void RoundTrip_BigSave_PreservesAllFields()
    {
        var original = SaveFile.Read(BigSavePath);
        byte[] serialized = original.Serialize();
        var reread = SaveFile.Parse(serialized);

        Assert.Equal(original.Sol, reread.Sol);
        // Counter count must match exactly
        var origCounters = original.EnumerateCounters().ToList();
        var rereadCounters = reread.EnumerateCounters().ToList();
        Assert.Equal(origCounters.Count, rereadCounters.Count);
        Assert.Equal(origCounters, rereadCounters);
        // Inventory count and per-item amounts
        Assert.Equal(original.Inventory.Count, reread.Inventory.Count);
        for (int i = 0; i < original.Inventory.Count; i++)
        {
            Assert.Equal(
                (string?)original.Inventory[i]!["EntityID"],
                (string?)reread.Inventory[i]!["EntityID"]);
            Assert.Equal(
                (int?)original.Inventory[i]!["Amount"],
                (int?)reread.Inventory[i]!["Amount"]);
        }
    }

    [Fact]
    public void Sol_Setter_UpdatesBothHeaderAndCounter()
    {
        var save = SaveFile.Read(BigSavePath);
        save.Sol = 999;
        Assert.Equal(999, (int?)save.Header["SummaryData"]!["Sol"]);
        Assert.True(save.TryGetCounter("stats.money", out int money));
        Assert.Equal(999, money);
    }

    [Fact]
    public void Inventory_Edit_SurvivesRoundTrip()
    {
        var save = SaveFile.Read(BigSavePath);
        int nicotineIdx = -1;
        for (int i = 0; i < save.Inventory.Count; i++)
        {
            if ((string?)save.Inventory[i]!["EntityID"] == "weak_nicotine_1")
            {
                nicotineIdx = i;
                break;
            }
        }
        Assert.NotEqual(-1, nicotineIdx);
        save.Inventory[nicotineIdx]!["Amount"] = 42;

        var reread = SaveFile.Parse(save.Serialize());
        Assert.Equal(42, (int?)reread.Inventory[nicotineIdx]!["Amount"]);
    }

    [Fact]
    public void Counter_Edit_SurvivesRoundTrip()
    {
        var save = SaveFile.Read(BigSavePath);
        save.SetCounter("skills.muscle", 5);
        var reread = SaveFile.Parse(save.Serialize());
        Assert.True(reread.TryGetCounter("skills.muscle", out int muscle));
        Assert.Equal(5, muscle);
    }

    [Fact]
    public void CorruptedHeaderChecksum_ThrowsSaveChecksumException()
    {
        byte[] data = File.ReadAllBytes(SmallSavePath);
        // The header hash is a 32-char string written some way into the file;
        // flip a character so it no longer matches.
        int idx = Array.IndexOf(data, (byte)'1', 945);  // first char of header hash
        Assert.NotEqual(-1, idx);
        data[idx] ^= 1;
        Assert.Throws<SaveChecksumException>(() => SaveFile.Parse(data));
    }

    private static void AssertHashesMatch(string path, string expectedHeader, string expectedContent)
    {
        // Re-implement minimal parsing to grab the stored hashes alongside
        // the raw blobs they cover, then verify Hash128.Compute matches.
        byte[] data = File.ReadAllBytes(path);
        using var ms = new MemoryStream(data);
        using var br = new BinaryReader(ms);

        string headerTag = br.ReadString();
        int headerVer = br.ReadInt32();
        int headerLen = br.ReadInt32();
        byte[] headerBlob = br.ReadBytes(headerLen);
        string headerHashFile = br.ReadString();
        byte _enc = br.ReadByte();
        string contentTag = br.ReadString();
        int contentVer = br.ReadInt32();
        int contentLen = br.ReadInt32();
        byte[] contentBlob = br.ReadBytes(contentLen);
        string contentHashFile = br.ReadString();

        Assert.Equal(expectedHeader, headerHashFile);
        Assert.Equal(expectedContent, contentHashFile);
        Assert.Equal(headerHashFile, Hash128.Compute(headerTag, headerVer, headerBlob));
        Assert.Equal(contentHashFile, Hash128.Compute(contentTag, contentVer, contentBlob));
    }
}
