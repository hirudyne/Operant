using System.Text.Json.Nodes;
using Operant.SaveFormat;

namespace Operant.Editor.ViewModels;

/// <summary>
/// Wraps a single (key, value) entry in FELDState.m_countersValues. Because the
/// game stores this dictionary as two parallel arrays, the viewmodel holds the
/// arrays and an index rather than the value directly.
/// </summary>
public class CounterViewModel : ViewModelBase
{
    private readonly JsonArray _keys;
    private readonly JsonArray _values;
    private readonly int _index;
    private readonly Action<string, int> _onChanged;

    public CounterViewModel(JsonArray keys, JsonArray values, int index, Action<string, int> onChanged)
    {
        _keys = keys;
        _values = values;
        _index = index;
        _onChanged = onChanged;
    }

    public string Key => (string?)_keys[_index] ?? string.Empty;

    /// <summary>
    /// User-facing label. For skill counters, looks up the display name from
    /// <see cref="SkillMetadata"/>; for demotion/promotion flags, shows the
    /// underlying skill's display name with a prefix; otherwise returns the
    /// raw key unchanged.
    /// </summary>
    public string DisplayKey
    {
        get
        {
            const string skillsPrefix = "skills.";
            if (!Key.StartsWith(skillsPrefix, StringComparison.Ordinal)) return Key;
            string remainder = Key[skillsPrefix.Length..];

            if (remainder.StartsWith("demotion_", StringComparison.Ordinal))
                return "Demoted: " + SkillMetadata.DisplayNameFor(remainder["demotion_".Length..]);
            if (remainder.StartsWith("promotion_", StringComparison.Ordinal))
                return "Promoted: " + SkillMetadata.DisplayNameFor(remainder["promotion_".Length..]);
            if (remainder.StartsWith("max_rating_", StringComparison.Ordinal))
                return SkillMetadata.DisplayNameFor(remainder["max_rating_".Length..]) + " (max rating)";
            if (remainder.StartsWith("penalty_", StringComparison.Ordinal))
                return SkillMetadata.DisplayNameFor(remainder["penalty_".Length..]) + " (penalty)";

            var skill = SkillMetadata.FromSaveId(remainder);
            return skill is not null ? skill.DisplayName : Key;
        }
    }

    public string Group
    {
        get
        {
            int dot = Key.IndexOf('.');
            return dot >= 0 ? Key[..dot] : "(other)";
        }
    }

    public int Value
    {
        get => (int?)_values[_index] ?? 0;
        set
        {
            if (Value == value) return;
            _values[_index] = value;
            OnPropertyChanged();
            _onChanged(Key, value);
        }
    }
}
