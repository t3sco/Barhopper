using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;

namespace Barhopper;

public sealed class ImageOverlay
{
    private const float ImageScale = 0.8f;

    private readonly Configuration configuration;
    private readonly ShortcutBar bar;
    private readonly ITextureProvider textures;
    private readonly IGameGui gameGui;

    public ImageOverlay(Configuration configuration, ShortcutBar bar, ITextureProvider textures, IGameGui gameGui)
    {
        this.configuration = configuration;
        this.bar = bar;
        this.textures = textures;
        this.gameGui = gameGui;
    }

    public void Draw(float opacity)
    {
        if (!configuration.CustomImages || !bar.HasImages || opacity <= 0f)
        {
            return;
        }

        var tint = ((uint)MathF.Round(opacity * 255f) << 24) | 0x00FFFFFFu;

        var addon = gameGui.GetAddonByName("_DTR");
        if (addon.IsNull || !addon.IsVisible)
        {
            return;
        }

        var drawList = ImGui.GetBackgroundDrawList();
        foreach (var shortcut in configuration.Shortcuts)
        {
            if (!bar.TryGetImageEntry(shortcut.Id, out var entry) || !entry.Shown || entry.UserHidden)
            {
                continue;
            }

            var texture = textures.GetFromFileAbsolute(shortcut.ImagePath).GetWrapOrEmpty();
            var (min, max) = entry.ScreenBounds;
            var size = max - min;
            var side = size.Y * ImageScale;
            var left = shortcut.ShowLabel ? min.X : min.X + ((size.X - side) * 0.5f);
            var top = min.Y + ((size.Y - side) * 0.5f);
            drawList.AddImage(texture.Handle, new Vector2(left, top), new Vector2(left + side, top + side), Vector2.Zero, Vector2.One, tint);
        }
    }
}
