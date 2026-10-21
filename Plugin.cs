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
    private readonly BarFader fader;
    private readonly MenuPopup menuPopup;
    private readonly GameMenuHider gameMenuHider;
    private readonly ConfigWindow configWindow;
    private readonly WindowSystem windowSystem = new("Barhopper");

    public Plugin(IDalamudPluginInterface pluginInterface, IDtrBar dtrBar, ICommandManager commandManager, ITextureProvider textureProvider, IGameGui gameGui, IFramework framework, IDataManager dataManager, INotificationManager notifications, IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        var savedConfiguration = pluginInterface.GetPluginConfig() as Configuration;
        configuration = savedConfiguration ?? Configuration.CreateDefault(pluginInterface.InternalName);
        var menu = new MainMenuData(dataManager);
        menuPopup = new MenuPopup(menu, framework);
        bar = new ShortcutBar(pluginInterface, dtrBar, commandManager, log, configuration, menuPopup);
        overlay = new ImageOverlay(configuration, bar, textureProvider, gameGui);
        fader = new BarFader(framework, gameGui, dtrBar, configuration);
        gameMenuHider = new GameMenuHider(framework, gameGui, configuration);
        configWindow = new ConfigWindow(pluginInterface, textureProvider, configuration, bar, menu, Save);
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
        WhatsNew.ShowIfUpdated(pluginInterface, notifications, configuration, savedConfiguration == null);
    }

    public void Dispose()
    {
        pluginInterface.ActivePluginsChanged -= OnActivePluginsChanged;
        pluginInterface.UiBuilder.OpenConfigUi -= configWindow.Toggle;
        pluginInterface.UiBuilder.OpenMainUi -= configWindow.Toggle;
        pluginInterface.UiBuilder.Draw -= Draw;
        commandManager.RemoveHandler(CommandName);
        windowSystem.RemoveAllWindows();
        gameMenuHider.Dispose();
        fader.Dispose();
        bar.Dispose();
    }

    private void Draw()
    {
        fader.TrackCursor(menuPopup.IsOpen);
        windowSystem.Draw();
        menuPopup.Draw();
        overlay.Draw(fader.Opacity);
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
