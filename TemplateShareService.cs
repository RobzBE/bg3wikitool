using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace BG3ItemExplorer;

internal static class TemplateShareService
{
    public const string Prefix = "BG3T1.";
    private const string LinkPrefix = "https://github.com/RobzBE/bg3wikitool#template=";
    private const int MaxEncodedLength = 100_000;
    private const int MaxDecodedLength = 1_000_000;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
        MaxDepth = 64
    };

    public static string ExportId(CharacterState state)
    {
        using var compressed = new MemoryStream();
        using (var brotli = new BrotliStream(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
            JsonSerializer.Serialize(brotli, state, JsonOptions);
        return Prefix + Convert.ToBase64String(compressed.ToArray())
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    public static string ExportLink(CharacterState state) => LinkPrefix + ExportId(state);

    public static CharacterState Import(string input)
    {
        var encoded = ExtractId(input);
        if (!encoded.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            throw new FormatException("The template ID does not start with BG3T1.");
        if (encoded.Length > MaxEncodedLength)
            throw new FormatException("The template ID is too large.");

        var base64 = encoded[Prefix.Length..].Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + ((4 - base64.Length % 4) % 4), '=');
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (FormatException exception)
        {
            throw new FormatException("The template ID contains invalid data.", exception);
        }

        try
        {
            using var source = new MemoryStream(bytes, writable: false);
            using var brotli = new BrotliStream(source, CompressionMode.Decompress);
            using var decoded = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = brotli.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (decoded.Length + read > MaxDecodedLength)
                    throw new FormatException("The decoded template is too large.");
                decoded.Write(buffer, 0, read);
            }
            decoded.Position = 0;
            var state = JsonSerializer.Deserialize<CharacterState>(decoded, JsonOptions)
                        ?? throw new FormatException("The template does not contain character data.");
            state.NormalizeClassLevels(!state.Difficulty.Equals("Explorer", StringComparison.OrdinalIgnoreCase));
            return state;
        }
        catch (Exception exception) when (exception is InvalidDataException or JsonException)
        {
            throw new FormatException("The template ID contains invalid compressed character data.", exception);
        }
    }

    public static void CopyInto(CharacterState target, CharacterState source)
    {
        target.TemplateId = source.TemplateId;
        target.Name = source.Name;
        target.Race = source.Race;
        target.ClassName = source.ClassName;
        target.Difficulty = source.Difficulty;
        target.Level = source.Level;
        target.ClassLevels = new Dictionary<string, int>(source.ClassLevels, StringComparer.OrdinalIgnoreCase);
        target.Strength = source.Strength;
        target.Dexterity = source.Dexterity;
        target.Constitution = source.Constitution;
        target.Intelligence = source.Intelligence;
        target.Wisdom = source.Wisdom;
        target.Charisma = source.Charisma;
        target.EquippedKeys = [.. source.EquippedKeys];
        target.DisabledConditionalEffects = [.. source.DisabledConditionalEffects];
        target.EnabledConditionalEffects = [.. source.EnabledConditionalEffects];
        target.FightingStyles = new Dictionary<string, string>(source.FightingStyles, StringComparer.OrdinalIgnoreCase);
        target.Feats = source.Feats.Select(feat => new FeatSelection { Name = feat.Name, Choice = feat.Choice }).ToList();
        target.ActiveBuffs = [.. source.ActiveBuffs];
        target.PermanentBonuses = source.PermanentBonuses
            .Select(bonus => new PermanentBonusSelection { Name = bonus.Name, Choice = bonus.Choice })
            .ToList();
    }

    private static string ExtractId(string input)
    {
        var value = (input ?? "").Trim();
        var marker = value.IndexOf("#template=", StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
            value = value[(marker + "#template=".Length)..];
        var ampersand = value.IndexOf('&');
        if (ampersand >= 0)
            value = value[..ampersand];
        return Uri.UnescapeDataString(value.Trim());
    }
}
