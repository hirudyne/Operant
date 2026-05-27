using Xunit;

namespace Operant.SaveFormat.Tests;

public class SkillFlagTests
{
    private const string BigSavePath = "TestData/0242cac8-d7f9-4a0d-9fbc-cc01765aad2f.sav";

    [Fact]
    public void DemotedSkill_ReadsExistingFlag()
    {
        var save = SaveFile.Read(BigSavePath);
        // The big save was created with vigour demoted.
        Assert.Equal("vigour", save.DemotedSkill);
    }

    [Fact]
    public void PromotedSkill_ReadsExistingFlag()
    {
        var save = SaveFile.Read(BigSavePath);
        Assert.Equal("focus", save.PromotedSkill);
    }

    [Fact]
    public void SetDemotedSkill_ReplacesPreviousFlag()
    {
        var save = SaveFile.Read(BigSavePath);
        save.DemotedSkill = "muscle";
        Assert.Equal("muscle", save.DemotedSkill);

        // The previous vigour flag should be gone; the new one present.
        Assert.False(save.TryGetCounter("skills.demotion_vigour", out _));
        Assert.True(save.TryGetCounter("skills.demotion_muscle", out int v));
        Assert.Equal(1, v);
    }

    [Fact]
    public void SetDemotedSkill_Null_ClearsFlag()
    {
        var save = SaveFile.Read(BigSavePath);
        save.DemotedSkill = null;
        Assert.Null(save.DemotedSkill);
        Assert.False(save.TryGetCounter("skills.demotion_vigour", out _));
    }

    [Fact]
    public void SkillFlags_RoundTripThroughSerialisation()
    {
        var save = SaveFile.Read(BigSavePath);
        save.DemotedSkill = "muscle";
        save.PromotedSkill = "presence";

        var reread = SaveFile.Parse(save.Serialize());
        Assert.Equal("muscle", reread.DemotedSkill);
        Assert.Equal("presence", reread.PromotedSkill);
    }

    [Fact]
    public void SkillMetadata_ContainsExpectedCanonicalIds()
    {
        // Quick sanity: a few known canonical ids must be present.
        Assert.Contains("coordination", SkillMetadata.SkillIds);
        Assert.Contains("focus",        SkillMetadata.SkillIds);
        Assert.Contains("vigour",       SkillMetadata.SkillIds);
        Assert.DoesNotContain("foo",    SkillMetadata.SkillIds);
    }
}
