namespace Operant.SaveFormat;

/// <summary>
/// Canonical skill identifiers used by the game's counter system. These are
/// the strings that appear as <c>skills.&lt;id&gt;</c>, <c>skills.demotion_&lt;id&gt;</c>,
/// and <c>skills.promotion_&lt;id&gt;</c> counter keys.
/// </summary>
public static class SkillMetadata
{
    public const string DemotionPrefix = "skills.demotion_";
    public const string PromotionPrefix = "skills.promotion_";

    public enum Faculty { Action, Relations, Intellect }

    public sealed record Skill(string SaveId, string DisplayName, Faculty Faculty);

    /// <summary>
    /// The 15 live skills (3 faculties of 5 each). The <c>SaveId</c> is the
    /// internal lowercase name used in <c>skills.&lt;id&gt;</c> counter keys
    /// and as the ScriptableObject filename in the addressables bundle. The
    /// <c>DisplayName</c> is the user-facing label from each skill asset's
    /// <c>EntityName</c> field, which is what the in-game character sheet
    /// shows (e.g. save key "presence" is displayed as "Shadowplay").
    /// </summary>
    /// <remarks>
    /// Internal and display names disagree in 12 of 15 cases - probably a
    /// vestige of an old design where skill ids matched display names, with
    /// the labels later reshuffled while the internal ids stayed put.
    /// Extracted from <c>Assets/ScriptableObjects/CMS/Skills/*.asset</c> in
    /// the addressables bundle.
    /// </remarks>
    public static readonly IReadOnlyList<Skill> Skills = new Skill[]
    {
        // Faculty of Action (physical)
        new("coordination", "Coordination",  Faculty.Action),
        new("vigour",       "Doppelg\u00e4ng", Faculty.Action),
        new("muscle",       "Instincts",     Faculty.Action),
        new("senses",       "Sensors",       Faculty.Action),
        new("presence",     "Shadowplay",    Faculty.Action),

        // Faculty of Relations (social)
        new("motivation",   "Blueprints",    Faculty.Relations),
        new("affect",       "Cold Read",     Faculty.Relations),
        new("nerve",        "Nerve",         Faculty.Relations),
        new("wits",         "Personalism",   Faculty.Relations),
        new("awareness",    "Statehood",     Faculty.Relations),

        // Faculty of Intellect (intellectual)
        new("entanglement", "Entanglement",  Faculty.Intellect),
        new("focus",        "Grey Matter",   Faculty.Intellect),
        new("inspiration",  "Poetics",       Faculty.Intellect),
        new("recall",       "Records",       Faculty.Intellect),
        new("inference",    "Technoflex",    Faculty.Intellect),
    };

    public static readonly IReadOnlyList<string> SkillIds =
        Skills.Select(s => s.SaveId).ToArray();

    public static Skill? FromSaveId(string saveId) =>
        Skills.FirstOrDefault(s => s.SaveId == saveId);

    public static string DisplayNameFor(string saveId) =>
        FromSaveId(saveId)?.DisplayName ?? saveId;
}
