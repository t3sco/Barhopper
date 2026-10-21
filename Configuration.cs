using Dalamud.Configuration;
using Dalamud.Game.Text;

namespace Barhopper;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;
    public bool CustomImages { get; set; }
    public bool AutoHideBar { get; set; }
    public float BarOpacity { get; set; } = 1f;
    public bool HideGameMenu { get; set; }
    public string LastSeenVersion { get; set; } = string.Empty;
    public List<Shortcut> Shortcuts { get; set; } = [];

    public static Shortcut CreateMenuShortcut(uint categoryId, string label)
    {
        var glyph = SeIconChar.Hexagon;
        if (categoryId != MenuPopup.WholeMenu && label.Length > 0 && char.IsAsciiLetter(label[0]))
        {
            Enum.TryParse($"BoxedLetter{char.ToUpperInvariant(label[0])}", out glyph);
        }

        return new Shortcut
        {
            Label = label,
            Action = ShortcutAction.GameMenu,
            MenuCategory = categoryId,
            Glyph = glyph,
        };
    }

    public static Shortcut CreateSelfShortcut(string internalName)
    {
        return new Shortcut
        {
            Label = "Barhopper",
            PluginInternalName = internalName,
            Action = ShortcutAction.Settings,
            Glyph = SeIconChar.BoxedPlus,
        };
    }

    public static Configuration CreateDefault(string internalName)
    {
        return new Configuration
        {
            Shortcuts =
            [
                CreateSelfShortcut(internalName),
                new Shortcut
                {
                    Label = "Penumbra",
                    PluginInternalName = "Penumbra",
                    Action = ShortcutAction.Command,
                    Command = "/penumbra",
                    Glyph = SeIconChar.BoxedLetterP,
                },
                new Shortcut
                {
                    Label = "Glamourer",
                    PluginInternalName = "Glamourer",
                    Action = ShortcutAction.Command,
                    Command = "/glamourer",
                    Glyph = SeIconChar.Glamoured,
                },
            ],
        };
    }
}
