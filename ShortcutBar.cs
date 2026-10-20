using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using LuminaStringBuilder = Lumina.Text.SeStringBuilder;

namespace Barhopper;

public sealed class ShortcutBar : IDisposable
{
    private const string ImagePlaceholder = "　";
    private const string TitlePrefix = "Shortcut: ";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IDtrBar dtrBar;
    private readonly ICommandManager commandManager;
    private readonly IPluginLog log;
    private readonly Configuration configuration;
    private readonly MenuPopup menuPopup;
    private readonly Dictionary<Guid, IDtrBarEntry> entries = [];
    private readonly HashSet<Guid> imageShortcuts = [];
    private readonly List<Guid> staleIds = [];

    public ShortcutBar(IDalamudPluginInterface pluginInterface, IDtrBar dtrBar, ICommandManager commandManager, IPluginLog log, Configuration configuration, MenuPopup menuPopup)
    {
        this.pluginInterface = pluginInterface;
        this.dtrBar = dtrBar;
        this.commandManager = commandManager;
        this.log = log;
        this.configuration = configuration;
        this.menuPopup = menuPopup;
    }

    public bool HasImages => imageShortcuts.Count > 0;

    public bool TryGetImageEntry(Guid id, out IDtrBarEntry entry)
    {
        entry = null!;
        return imageShortcuts.Contains(id) && entries.TryGetValue(id, out entry!);
    }

    public void Refresh()
    {
        RemoveStaleEntries();
        imageShortcuts.Clear();
        foreach (var shortcut in configuration.Shortcuts)
        {
            var entry = EntryFor(shortcut);
            if (entry == null)
            {
                continue;
            }

            var plugin = FindPlugin(shortcut.PluginInternalName);
            var usesImage = UsesImage(shortcut);
            if (usesImage)
            {
                imageShortcuts.Add(shortcut.Id);
            }

            entry.Text = BuildText(shortcut, usesImage);
            entry.Tooltip = plugin is { HasConfigUi: true } && shortcut.Action != ShortcutAction.Settings
                ? $"{shortcut.Label}\nRight-click for its settings"
                : shortcut.Label;
            entry.OnClick = interaction => Activate(shortcut, interaction);
            entry.Shown = shortcut.PluginInternalName.Length == 0 || plugin != null;
        }
    }

    public void Dispose()
    {
        foreach (var entry in entries.Values)
        {
            entry.Remove();
        }

        entries.Clear();
    }

    public IExposedPlugin? FindPlugin(string internalName)
    {
        if (internalName.Length == 0)
        {
            return null;
        }

        foreach (var plugin in pluginInterface.InstalledPlugins)
        {
            if (plugin.IsLoaded && plugin.InternalName == internalName)
            {
                return plugin;
            }
        }

        return null;
    }

    private IDtrBarEntry? EntryFor(Shortcut shortcut)
    {
        var title = TitlePrefix + shortcut.Label;
        if (entries.TryGetValue(shortcut.Id, out var existing))
        {
            if (existing.Title == title)
            {
                return existing;
            }

            existing.Remove();
            entries.Remove(shortcut.Id);
        }

        try
        {
            var entry = dtrBar.Get(title);
            entries[shortcut.Id] = entry;
            return entry;
        }
        catch (Exception exception)
        {
            log.Warning(exception, $"Could not add the bar entry \"{title}\"");
            return null;
        }
    }

    private void RemoveStaleEntries()
    {
        staleIds.Clear();
        foreach (var id in entries.Keys)
        {
            if (!configuration.Shortcuts.Exists(shortcut => shortcut.Id == id))
            {
                staleIds.Add(id);
            }
        }

        foreach (var id in staleIds)
        {
            entries[id].Remove();
            entries.Remove(id);
        }
    }

    private bool UsesImage(Shortcut shortcut)
    {
        return configuration.CustomImages
            && shortcut.IconKind == IconKind.Image
            && shortcut.ImagePath.Length > 0
            && File.Exists(shortcut.ImagePath);
    }

    private static SeString BuildText(Shortcut shortcut, bool usesImage)
    {
        var builder = new LuminaStringBuilder();
        var hasTextColour = shortcut.TextColour.W > 0f;
        var hasGlowColour = shortcut.GlowColour.W > 0f;
        if (hasTextColour)
        {
            builder.PushColorRgba(shortcut.TextColour);
        }

        if (hasGlowColour)
        {
            builder.PushEdgeColorRgba(shortcut.GlowColour);
        }

        if (usesImage)
        {
            builder.Append(ImagePlaceholder);
        }
        else if (shortcut.IconKind == IconKind.GameIcon)
        {
            builder.AppendIcon((uint)shortcut.GameIcon);
        }
        else
        {
            builder.Append(shortcut.Glyph.ToIconString());
        }

        if (shortcut.ShowLabel && shortcut.Label.Length > 0)
        {
            builder.Append(" " + shortcut.Label);
        }

        if (hasGlowColour)
        {
            builder.PopEdgeColor();
        }

        if (hasTextColour)
        {
            builder.PopColor();
        }

        return SeString.Parse(builder.ToArray());
    }

    private void Activate(Shortcut shortcut, DtrInteractionEvent interaction)
    {
        var plugin = FindPlugin(shortcut.PluginInternalName);
        try
        {
            if (interaction.ClickType == MouseClickType.Right && plugin is { HasConfigUi: true })
            {
                plugin.OpenConfigUi();
                return;
            }

            switch (shortcut.Action)
            {
                case ShortcutAction.Command when shortcut.Command.Length > 0:
                    commandManager.ProcessCommand(shortcut.Command);
                    break;
                case ShortcutAction.GameMenu when entries.TryGetValue(shortcut.Id, out var entry):
                    menuPopup.Toggle(shortcut.MenuCategory, entry.ScreenBounds);
                    break;
                case ShortcutAction.Settings when plugin is { HasConfigUi: true }:
                    plugin.OpenConfigUi();
                    break;
                case ShortcutAction.MainWindow when plugin is { HasMainUi: true }:
                    plugin.OpenMainUi();
                    break;
                case ShortcutAction.MainWindow when plugin is { HasConfigUi: true }:
                    plugin.OpenConfigUi();
                    break;
            }
        }
        catch (Exception exception)
        {
            log.Error(exception, $"The \"{shortcut.Label}\" shortcut failed");
        }
    }
}
