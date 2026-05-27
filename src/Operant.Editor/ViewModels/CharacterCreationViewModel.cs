using Operant.SaveFormat;

namespace Operant.Editor.ViewModels;

/// <summary>
/// Edits the demotion / promotion flags set during character creation. Each is
/// a skill id (or null = "no skill"). Setting one will clear any other skill
/// flagged for the same role.
/// </summary>
public class CharacterCreationViewModel : ViewModelBase
{
    private readonly SaveFile _save;
    private readonly Action _markDirty;

    public CharacterCreationViewModel(SaveFile save, Action markDirty)
    {
        _save = save;
        _markDirty = markDirty;
    }

    /// <summary>"(none)" sentinel plus the canonical skill ids, in the order the UI displays them.</summary>
    public IReadOnlyList<SkillChoice> SkillChoices { get; } =
        new[] { new SkillChoice(null, "(none)") }
        .Concat(SkillMetadata.SkillIds.Select(id => new SkillChoice(id, id)))
        .ToArray();

    public string? DemotedSkill
    {
        get => _save.DemotedSkill;
        set
        {
            if (_save.DemotedSkill == value) return;
            _save.DemotedSkill = value;
            OnPropertyChanged();
            _markDirty();
        }
    }

    public string? PromotedSkill
    {
        get => _save.PromotedSkill;
        set
        {
            if (_save.PromotedSkill == value) return;
            _save.PromotedSkill = value;
            OnPropertyChanged();
            _markDirty();
        }
    }
}

public record SkillChoice(string? Value, string Label)
{
    public override string ToString() => Label;
}
