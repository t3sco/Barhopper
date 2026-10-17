using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Barhopper;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/barhopper";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly Configuration configuration;
    private readonly ShortcutBar bar;
    private readonly ImageOverlay overlay;
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem windowSystem = new("Barhopper");

    public Plugin(IDalamudPluginInterface pluginInterface, IDtrBar dtrBar, ICommandManager commandManager, ITextureProvider textureProvider, IGameGui gameGui, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        configuration = pluginInterface.GetPluginConfig() as Configuration ?? Configuration.CreateDefault(pluginInterface.InternalName);
        bar = new ShortcutBar(pluginInterface, dtrBar, commandManager, log, configuration);
        overlay = new ImageOverlay(configuration, bar, textureProvider, gameGui);
        configWindow = new ConfigWindow(pluginInterface, textureProvider, configuration, bar, Save);
        windowSystem.AddWindow(configWindow);

        commandManager.AddHandler(CommandName, new CommandInfo((_, _) => configWindow.Toggle())
        {
            HelpMessage = "Open the Barhopper settings.",
        });
        pluginInterface.UiBuilder.Draw += Draw;
        pluginInterface.UiBuilder.OpenMainUi += configWindow.Toggle;
        pluginInterface.UiBuilder.OpenConfigUi += configWindow.Toggle;
        pluginInterface.ActivePluginsChanged += OnActivePluginsChanged;
        bar.Refresh();
    }

    public void Dispose()
    {
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
        pluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        pluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        pluginInterface.UiBuilder.Draw -= Draw;
        commandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        bar.Dispose();
    }

    private void Draw()
    {
        windowSystem.Draw();
        overlay.Draw();
    }

    private void Save()
    {
        pluginInterface.SavePluginConfig(configuration);
        bar.Refresh();
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args)
    {
        bar.Refresh();
    }
}
