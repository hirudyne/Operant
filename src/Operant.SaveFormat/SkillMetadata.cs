namespace Operant.SaveFormat;

/// <summary>
/// Canonical skill identifiers used by the game's counter system. These are
/// the strings that appear as <c>skills.&lt;id&gt;</c>, <c>skills.demotion_&lt;id&gt;</c>,
/// and <c>skills.promotion_&lt;id&gt;</c> counter keys.
/// </summary>
/// <remarks>
/// 15 skills, three faculties of five. The list was cross-checked against
/// real saves: every distinct <c>skills.&lt;id&gt;</c> counter across the
/// available sample saves appears below, and nothing else does. The IL2CPP
/// string-literal table contains additional plausible-looking strings (e.g.
/// "shadowplay") that turn out to be dialogue/thought identifiers rather than
/// skill ids, so we don't trust string-table matches alone.
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
        "vigour",
        "wits",
    };

    public const string DemotionPrefix = "skills.demotion_";
    public const string PromotionPrefix = "skills.promotion_";
}
