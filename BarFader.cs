using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Barhopper;

public sealed unsafe class BarFader : IDisposable
{
    public const float MinimumOpacity = 0.1f;
    private const string BarAddonName = "_DTR";
    private const float NearMargin = 48f;
    private const float FadeSeconds = 0.08f;
    private const float SnapDistance = 0.01f;

    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IDtrBar dtrBar;
    private readonly Configuration configuration;
    private float fade = 1f;
    private bool cursorNear = true;
    private bool alphaApplied;

    public BarFader(IFramework framework, IGameGui gameGui, IDtrBar dtrBar, Configuration configuration)
    {
        this.framework = framework;
        this.gameGui = gameGui;
        this.dtrBar = dtrBar;
        this.configuration = configuration;
        framework.Update += OnUpdate;
    }

    public float Opacity => Math.Clamp(configuration.BarOpacity, MinimumOpacity, 1f) * fade;

    public void TrackCursor(bool keepVisible)
    {
        if (!configuration.AutoHideBar || keepVisible)
        {
            cursorNear = true;
            return;
        }

        var addon = gameGui.GetAddonByName(BarAddonName);
        if (addon.IsNull)
        {
            return;
        }

        var min = addon.Position;
        var max = addon.Position + addon.ScaledSize;
        var entries = dtrBar.Entries;
        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            if (!entry.Shown || entry.UserHidden)
            {
                continue;
            }

            var (entryMin, entryMax) = entry.ScreenBounds;
            if (entryMax.X <= entryMin.X)
            {
                continue;
            }

            min = Vector2.Min(min, entryMin);
            max = Vector2.Max(max, entryMax);
        }

        var margin = new Vector2(NearMargin);
        min -= margin;
        max += margin;
        var mouse = ImGui.GetMousePos();
        cursorNear = mouse.X >= min.X && mouse.X <= max.X && mouse.Y >= min.Y && mouse.Y <= max.Y;
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        if (alphaApplied)
        {
            SetBarAlpha(1f);
        }
    }

    private void OnUpdate(IFramework updatedFramework)
    {
        var target = configuration.AutoHideBar && !cursorNear ? 0f : 1f;
        fade = Approach(fade, target, (float)updatedFramework.UpdateDelta.TotalSeconds);
        var opacity = Opacity;
        if (!alphaApplied && opacity >= 1f)
        {
            return;
        }

        if (SetBarAlpha(opacity))
        {
            alphaApplied = opacity < 1f;
        }
    }

    private bool SetBarAlpha(float opacity)
    {
        var addon = gameGui.GetAddonByName(BarAddonName);
        if (addon.IsNull)
        {
            return false;
        }

        ((AtkUnitBase*)addon.Address)->SetAlpha((byte)MathF.Round(opacity * 255f));
        return true;
    }

    private static float Approach(float current, float target, float deltaSeconds)
    {
        var next = target + ((current - target) * MathF.Exp(-deltaSeconds / FadeSeconds));
        return MathF.Abs(next - target) < SnapDistance ? target : next;
    }
}
