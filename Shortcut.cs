using System.Numerics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;

namespace Barhopper;

[Serializable]
public sealed class Shortcut
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;
    public string PluginInternalName { get; set; } = string.Empty;
    public ShortcutAction Action { get; set; } = ShortcutAction.Command;
    public string Command { get; set; } = string.Empty;
    public IconKind IconKind { get; set; } = IconKind.Glyph;
    public SeIconChar Glyph { get; set; } = SeIconChar.BoxedStar;
    public BitmapFontIcon GameIcon { get; set; } = BitmapFontIcon.GoldStar;
    public string ImagePath { get; set; } = string.Empty;
    public bool ShowLabel { get; set; }
    public Vector4 TextColour { get; set; }
    public Vector4 GlowColour { get; set; }
}

public enum ShortcutAction
{
    MainWindow,
    Settings,
    Command,
}

public enum IconKind
{
    Glyph,
    GameIcon,
    Image,
}
