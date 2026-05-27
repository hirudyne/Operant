namespace Operant.SaveFormat;

/// <summary>
/// Canonical skill identifiers used by the game's counter system. These are
/// the strings that appear as <c>skills.&lt;id&gt;</c>, <c>skills.demotion_&lt;id&gt;</c>,
/// and <c>skills.promotion_&lt;id&gt;</c> counter keys.
/// </summary>
/// <remarks>
/// Derived from the IL2CPP dump's string-literal table: the only strings
/// matching the per-skill counter format are the 15 listed below. Note that
/// the <c>SkillType</c> enum in <c>ZAUM.FELD.C4.Dialogues.Model.Entities</c>
/// uses older or dialogue-side names (e.g. "doppelgang" vs the save-side
/// "awareness"); the names here are the ones the save format actually uses.
/// </remarks>
public static class SkillMetadata
{
    public static readonly IReadOnlyList<string> SkillIds = new[]
    {
        "affect",
        "awareness",
        "coordination",
        "entanglement",
        "focus",
        "inference",
        "inspiration",
        "motivation",
        "muscle",
        "nerve",
        "presence",
        "recall",
        "senses",
        "shadowplay",
        "vigour",
        "wits",
    };

    public const string DemotionPrefix = "skills.demotion_";
    public const string PromotionPrefix = "skills.promotion_";
}
