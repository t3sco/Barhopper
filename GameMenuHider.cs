using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace Barhopper;

public sealed unsafe class GameMenuHider : IDisposable
{
    private const string MenuAddonName = "_MainCommand";

    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly Configuration configuration;
    private bool hidden;

    public GameMenuHider(IFramework framework, IGameGui gameGui, Configuration configuration)
    {
        this.framework = framework;
        this.gameGui = gameGui;
        this.configuration = configuration;
        framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        framework.Update -= OnUpdate;
        if (hidden)
        {
            SetVisible(true);
        }
    }

    private void OnUpdate(IFramework updatedFramework)
    {
        if (configuration.HideGameMenu)
        {
            hidden |= SetVisible(false);
        }
        else if (hidden)
        {
            SetVisible(true);
            hidden = false;
        }
    }

    private bool SetVisible(bool visible)
    {
        var addon = gameGui.GetAddonByName(MenuAddonName);
        if (addon.IsNull)
        {
            return false;
        }

        var unit = (AtkUnitBase*)addon.Address;
        if (unit->IsVisible != visible)
        {
            unit->IsVisible = visible;
        }

        return true;
    }
}
