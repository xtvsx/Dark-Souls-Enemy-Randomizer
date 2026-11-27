using System.Globalization;
using EnemyRandomizer.Core.Configuration;

namespace EnemyRandomizer.Core.Settings;

/// <summary>
/// The shareable text config of v0.4.x: option values ordered by <see cref="OptionDefinition.LegacyIndex"/>,
/// separated by <c>/-/</c>, followed by the seed in triple quotes (legacy <c>randomizer.py:1507-1635</c>).
/// Example: <c>1/-/2/-/0/-/.../-/1/-/'''my seed'''</c>. Strings from v0.4.x stay compatible.
/// </summary>
public static class LegacyConfigString
{
    private const string Separator = "/-/";
    private const string SeedQuote = "'''";

    /// <summary>Number of options of v0.4.x config strings (older strings stay valid, see <see cref="TryParse"/>).</summary>
    private const int OldestOptionCount = 21;

    public static string Format(RandomizerSettings settings, RandomizerOptionSettings options)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(options);

        var values = options.Options
            .OrderBy(o => o.LegacyIndex)
            .Select(o => settings[o.Key].ToString(CultureInfo.InvariantCulture));

        return string.Join(Separator, values.Append(SeedQuote + settings.Seed + SeedQuote));
    }

    /// <summary>
    /// Parses <paramref name="text"/>. The enemy config is not part of the text and is taken from
    /// <paramref name="enemyConfig"/>. On failure <paramref name="error"/> says what is wrong.
    /// </summary>
    public static bool TryParse(
        string text,
        RandomizerOptionSettings options,
        string enemyConfig,
        out RandomizerSettings? settings,
        out string error)
    {
        ArgumentNullException.ThrowIfNull(options);
        settings = null;

        var parts = (text ?? "").Trim().Split(Separator);
        var expected = options.Options.Count + 1;
        var oldest = OldestOptionCount + 1;
        if (parts.Length > expected || parts.Length < Math.Min(oldest, expected))
        {
            error = $"expected {expected} fields separated by '{Separator}', found {parts.Length}.";
            return false;
        }

        // Strings from older versions have fewer option fields: the missing (newer) options keep their default.
        var optionFields = parts.Length - 1;
        var values = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var option in options.Options)
        {
            if (option.LegacyIndex >= optionFields)
            {
                values[option.Key] = option.Default;
                continue;
            }

            var part = parts[option.LegacyIndex];
            if (!int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
                || !option.IsValidValue(option.Migrate(value)))
            {
                error = $"'{part}' is not a valid value for '{option.Title.TrimEnd(':')}'.";
                return false;
            }

            values[option.Key] = option.Migrate(value);
        }

        var seed = parts[^1].Replace(SeedQuote, "", StringComparison.Ordinal);
        settings = new RandomizerSettings(values, seed, enemyConfig);
        error = "";
        return true;
    }
}
