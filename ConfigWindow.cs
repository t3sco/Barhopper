using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Barhopper;

public sealed class ConfigWindow : Window
{
    private const int GridColumns = 10;
    private const string ImageFilter = "Images{.png,.jpg,.jpeg,.bmp}";
    private const string WholeMenuLabel = "Whole menu (one button)";

    private static readonly SeIconChar[] Glyphs = BuildGlyphs();
    private static readonly string[] GlyphTexts = Array.ConvertAll(Glyphs, glyph => glyph.ToIconString());
    private static readonly string[] GlyphNames = Array.ConvertAll(Glyphs, glyph => glyph.ToString());
    private static readonly BitmapFontIcon[] GameIcons = BuildGameIcons();
    private static readonly byte[][] GameIconTexts = Array.ConvertAll(GameIcons, icon => new SeStringBuilder().AddIcon(icon).Build().Encode());
    private static readonly string[] GameIconNames = Array.ConvertAll(GameIcons, icon => icon.ToString());

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ITextureProvider textures;
    private readonly Configuration configuration;
    private readonly ShortcutBar bar;
    private readonly MainMenuData menu;
    private readonly Action save;
    private readonly FileDialogManager fileDialog = new();
    private readonly List<IExposedPlugin> pluginChoices = [];
    private Guid removalId;
    private bool unsavedChanges;

    public ConfigWindow(IDalamudPluginInterface pluginInterface, ITextureProvider textures, Configuration configuration, ShortcutBar bar, MainMenuData menu, Action save)
        : base("Barhopper###BarhopperConfig")
    {
        this.pluginInterface = pluginInterface;
        this.textures = textures;
        this.configuration = configuration;
        this.bar = bar;
        this.menu = menu;
        this.save = save;
        Size = new Vector2(560, 520);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        DrawSelfShortcutToggle();
        DrawBarLook();
        DrawGameMenuSection();
        DrawCustomImagesToggle();
        ImGui.Separator();
        for (var index = 0; index < configuration.Shortcuts.Count; index++)
        {
            ImGui.PushID(index);
            DrawShortcut(configuration.Shortcuts[index]);
            ImGui.PopID();
        }

        if (removalId != Guid.Empty)
        {
            var id = removalId;
            configuration.Shortcuts.RemoveAll(shortcut => shortcut.Id == id);
            removalId = Guid.Empty;
            save();
        }

        ImGui.Spacing();
        if (ImGui.Button("Add shortcut"))
        {
            configuration.Shortcuts.Add(new Shortcut { Label = UniqueLabel("Shortcut", null) });
            save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Reorder or hide entries"))
        {
            pluginInterface.OpenDalamudSettingsTo(SettingsOpenKind.ServerInfoBar);
        }

        fileDialog.Draw();
        if (unsavedChanges && !ImGui.IsAnyItemActive())
        {
            unsavedChanges = false;
            save();
        }
    }

    public override void OnClose()
    {
        if (unsavedChanges)
        {
            unsavedChanges = false;
            save();
        }
    }

    private void DrawSelfShortcutToggle()
    {
        var internalName = pluginInterface.InternalName;
        var selfIndex = -1;
        for (var index = 0; index < configuration.Shortcuts.Count; index++)
        {
            if (configuration.Shortcuts[index].PluginInternalName == internalName)
            {
                selfIndex = index;
                break;
            }
        }

        var showSelf = selfIndex >= 0;
        if (!ImGui.Checkbox("Show a shortcut to these settings on the bar", ref showSelf))
        {
            return;
        }

        if (showSelf)
        {
            var shortcut = Configuration.CreateSelfShortcut(internalName);
            shortcut.Label = UniqueLabel(shortcut.Label, shortcut);
            configuration.Shortcuts.Insert(0, shortcut);
        }
        else
        {
            configuration.Shortcuts.RemoveAt(selfIndex);
        }

        save();
    }

    private void DrawBarLook()
    {
        var autoHide = configuration.AutoHideBar;
        if (ImGui.Checkbox("Hide the bar until the cursor is near it", ref autoHide))
        {
            configuration.AutoHideBar = autoHide;
            save();
        }

        var minimumPercent = (int)MathF.Round(BarFader.MinimumOpacity * 100f);
        var percent = (int)MathF.Round(configuration.BarOpacity * 100f);
        if (ImGui.SliderInt("Bar opacity", ref percent, minimumPercent, 100, "%d%%"))
        {
            configuration.BarOpacity = Math.Clamp(percent, minimumPercent, 100) / 100f;
            unsavedChanges = true;
        }

        ImGui.TextDisabled("Both apply to the whole server info bar, including the clock and other plugins' entries.");
    }

    private void DrawCustomImagesToggle()
    {
        var customImages = configuration.CustomImages;
        if (ImGui.Checkbox("Custom images (experimental)", ref customImages))
        {
            configuration.CustomImages = customImages;
            save();
        }

        ImGui.TextDisabled("The bar itself only shows game icons, so custom images are drawn on top of it.");
        ImGui.TextDisabled("They can show over game windows that overlap the bar.");
    }

    private void DrawShortcut(Shortcut shortcut)
    {
        if (!ImGui.CollapsingHeader($"{shortcut.Label}###header"))
        {
            return;
        }

        ImGui.Indent();
        DrawPluginCombo(shortcut);
        DrawAction(shortcut);
        DrawLabel(shortcut);
        DrawIcon(shortcut);
        DrawColours(shortcut);
        if (ImGui.Button("Remove shortcut"))
        {
            removalId = shortcut.Id;
        }

        ImGui.Unindent();
        ImGui.Spacing();
    }

    private void DrawPluginCombo(Shortcut shortcut)
    {
        var preview = shortcut.PluginInternalName.Length == 0
            ? "None (command only)"
            : bar.FindPlugin(shortcut.PluginInternalName)?.Name ?? $"{shortcut.PluginInternalName} (not loaded)";
        if (!ImGui.BeginCombo("Plugin", preview))
        {
            return;
        }

        if (ImGui.IsWindowAppearing())
        {
            LoadPluginChoices();
        }

        if (ImGui.Selectable("None (command only)", shortcut.PluginInternalName.Length == 0))
        {
            Link(shortcut, null);
        }

        foreach (var plugin in pluginChoices)
        {
            if (ImGui.Selectable(plugin.Name, plugin.InternalName == shortcut.PluginInternalName))
            {
                Link(shortcut, plugin);
            }
        }

        ImGui.EndCombo();
    }

    private void LoadPluginChoices()
    {
        pluginChoices.Clear();
        foreach (var plugin in pluginInterface.InstalledPlugins)
        {
            if (plugin.IsLoaded && (plugin.HasMainUi || plugin.HasConfigUi))
            {
                pluginChoices.Add(plugin);
            }
        }

        pluginChoices.Sort(static (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase));
    }

    private void Link(Shortcut shortcut, IExposedPlugin? plugin)
    {
        shortcut.PluginInternalName = plugin?.InternalName ?? string.Empty;
        if (plugin == null)
        {
            shortcut.Action = ShortcutAction.Command;
        }
        else
        {
            shortcut.Label = UniqueLabel(plugin.Name, shortcut);
            shortcut.Action = plugin.HasMainUi ? ShortcutAction.MainWindow : ShortcutAction.Settings;
        }

        save();
    }

    private void DrawAction(Shortcut shortcut)
    {
        if (shortcut.PluginInternalName.Length > 0)
        {
            DrawActionChoice(shortcut, "Open main window", ShortcutAction.MainWindow);
            ImGui.SameLine();
            DrawActionChoice(shortcut, "Open settings", ShortcutAction.Settings);
            ImGui.SameLine();
            DrawActionChoice(shortcut, "Run a command", ShortcutAction.Command);
        }
        else
        {
            DrawActionChoice(shortcut, "Run a command", ShortcutAction.Command);
            ImGui.SameLine();
            DrawActionChoice(shortcut, "Open the game menu", ShortcutAction.GameMenu);
        }

        if (shortcut.Action == ShortcutAction.GameMenu)
        {
            DrawMenuCategoryCombo(shortcut);
            return;
        }

        if (shortcut.Action != ShortcutAction.Command)
        {
            return;
        }

        var command = shortcut.Command;
        if (ImGui.InputTextWithHint("Command", "/penumbra", ref command, 128))
        {
            shortcut.Command = command;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            shortcut.Command = NormalizeCommand(command);
            save();
        }

        ImGui.TextDisabled("A command usually opens and closes the window; the other options only open it.");
    }

    private void DrawMenuCategoryCombo(Shortcut shortcut)
    {
        var current = menu.Find(shortcut.MenuCategory);
        if (!ImGui.BeginCombo("Menu", current?.Name ?? WholeMenuLabel))
        {
            return;
        }

        if (ImGui.Selectable(WholeMenuLabel, current == null))
        {
            shortcut.MenuCategory = MenuPopup.WholeMenu;
            save();
        }

        foreach (var category in menu.Categories)
        {
            if (ImGui.Selectable(category.Name, current == category))
            {
                shortcut.MenuCategory = category.Id;
                save();
            }
        }

        ImGui.EndCombo();
    }

    private void DrawGameMenuSection()
    {
        var menuShortcuts = 0;
        var wholeMenuButtons = 0;
        foreach (var shortcut in configuration.Shortcuts)
        {
            if (shortcut.Action == ShortcutAction.GameMenu)
            {
                menuShortcuts++;
                if (shortcut.MenuCategory == MenuPopup.WholeMenu)
                {
                    wholeMenuButtons++;
                }
            }
        }

        if (menuShortcuts == 0)
        {
            if (ImGui.Button("Add the game menu to the bar"))
            {
                ReplaceMenuShortcuts(false);
                configuration.HideGameMenu = true;
                save();
            }
        }
        else
        {
            var oneButton = menuShortcuts == 1 && wholeMenuButtons == 1;
            if (ImGui.Checkbox("One menu button instead of an icon per category", ref oneButton))
            {
                ReplaceMenuShortcuts(oneButton);
                save();
            }

            ImGui.SameLine();
            if (ImGui.SmallButton("Remove the game menu"))
            {
                ReplaceMenuShortcuts(null);
                configuration.HideGameMenu = false;
                save();
            }
        }

        var hideGameMenu = configuration.HideGameMenu;
        if (ImGui.Checkbox("Hide the game's own main menu", ref hideGameMenu))
        {
            configuration.HideGameMenu = hideGameMenu;
            save();
        }
    }

    private void ReplaceMenuShortcuts(bool? oneButton)
    {
        var insertAt = -1;
        for (var index = configuration.Shortcuts.Count - 1; index >= 0; index--)
        {
            if (configuration.Shortcuts[index].Action == ShortcutAction.GameMenu)
            {
                configuration.Shortcuts.RemoveAt(index);
                insertAt = index;
            }
        }

        if (oneButton == null)
        {
            return;
        }

        if (insertAt < 0)
        {
            insertAt = configuration.Shortcuts.Count;
        }

        if (oneButton.Value)
        {
            configuration.Shortcuts.Insert(insertAt, Configuration.CreateMenuShortcut(MenuPopup.WholeMenu, UniqueLabel("Menu", null)));
            return;
        }

        foreach (var category in menu.Categories)
        {
            configuration.Shortcuts.Insert(insertAt++, Configuration.CreateMenuShortcut(category.Id, UniqueLabel(category.Name, null)));
        }
    }

    private void DrawActionChoice(Shortcut shortcut, string label, ShortcutAction action)
    {
        if (ImGui.RadioButton(label, shortcut.Action == action))
        {
            shortcut.Action = action;
            save();
        }
    }

    private void DrawLabel(Shortcut shortcut)
    {
        var label = shortcut.Label;
        if (ImGui.InputText("Name", ref label, 64))
        {
            shortcut.Label = label;
        }

        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            shortcut.Label = UniqueLabel(label.Trim(), shortcut);
            save();
        }

        var showLabel = shortcut.ShowLabel;
        if (ImGui.Checkbox("Show the name on the bar", ref showLabel))
        {
            shortcut.ShowLabel = showLabel;
            save();
        }
    }

    private void DrawIcon(Shortcut shortcut)
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Icon");
        ImGui.SameLine();
        DrawIconPreview(shortcut);
        ImGui.SameLine();
        if (ImGui.Button("Change"))
        {
            ImGui.OpenPopup("icons");
        }

        if (!ImGui.BeginPopup("icons"))
        {
            return;
        }

        if (ImGui.BeginTabBar("kinds"))
        {
            if (ImGui.BeginTabItem("Flat"))
            {
                DrawGlyphGrid(shortcut);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("Coloured"))
            {
                DrawGameIconGrid(shortcut);
                ImGui.EndTabItem();
            }

            if (configuration.CustomImages && ImGui.BeginTabItem("Image"))
            {
                DrawImagePicker(shortcut);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        ImGui.EndPopup();
    }

    private void DrawIconPreview(Shortcut shortcut)
    {
        if (shortcut.IconKind == IconKind.Image && configuration.CustomImages && shortcut.ImagePath.Length > 0)
        {
            var texture = textures.GetFromFileAbsolute(shortcut.ImagePath).GetWrapOrEmpty();
            var side = ImGui.GetFrameHeight();
            ImGui.Image(texture.Handle, new Vector2(side, side));
            return;
        }

        if (shortcut.IconKind == IconKind.GameIcon)
        {
            var index = Array.IndexOf(GameIcons, shortcut.GameIcon);
            if (index >= 0)
            {
                ImGuiHelpers.SeStringWrapped(GameIconTexts[index]);
                return;
            }
        }

        var glyphIndex = Array.IndexOf(Glyphs, shortcut.Glyph);
        var glyphText = glyphIndex >= 0 ? GlyphTexts[glyphIndex] : "?";
        if (shortcut.TextColour.W > 0f)
        {
            ImGui.TextColored(shortcut.TextColour, glyphText);
            return;
        }

        ImGui.TextUnformatted(glyphText);
    }

    private void DrawColours(Shortcut shortcut)
    {
        var textColour = shortcut.TextColour;
        var glowColour = shortcut.GlowColour;
        var changed = DrawColour("Text colour", ref textColour);
        ImGui.SameLine();
        changed |= DrawColour("Glow colour", ref glowColour);
        if (shortcut.IconKind != IconKind.Glyph)
        {
            ImGui.TextDisabled("Colours apply to flat icons and names. Coloured icons and images keep their own colours.");
        }

        if (!changed)
        {
            return;
        }

        shortcut.TextColour = textColour;
        shortcut.GlowColour = glowColour;
        unsavedChanges = true;
        bar.Refresh();
    }

    private static bool DrawColour(string label, ref Vector4 colour)
    {
        ImGui.PushID(label);
        var isSet = colour.W > 0f;
        var rgb = isSet ? new Vector3(colour.X, colour.Y, colour.Z) : Vector3.One;
        var changed = ImGui.ColorEdit3("##colour", ref rgb, ImGuiColorEditFlags.NoInputs);
        if (changed)
        {
            colour = new Vector4(rgb, 1f);
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(label);
        ImGui.SameLine();
        if (!isSet)
        {
            ImGui.TextDisabled("(default)");
        }
        else if (ImGui.SmallButton("Reset"))
        {
            colour = Vector4.Zero;
            changed = true;
        }

        ImGui.PopID();
        return changed;
    }

    private void DrawGlyphGrid(Shortcut shortcut)
    {
        var side = ImGui.GetFrameHeight() * 1.4f;
        for (var index = 0; index < Glyphs.Length; index++)
        {
            if (index % GridColumns != 0)
            {
                ImGui.SameLine();
            }

            var selected = shortcut.IconKind == IconKind.Glyph && shortcut.Glyph == Glyphs[index];
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive));
            }

            ImGui.PushID(index);
            var clicked = ImGui.Button(GlyphTexts[index], new Vector2(side, side));
            ImGui.PopID();
            if (selected)
            {
                ImGui.PopStyleColor();
            }

            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(GlyphNames[index]);
            }

            if (clicked)
            {
                shortcut.IconKind = IconKind.Glyph;
                shortcut.Glyph = Glyphs[index];
                save();
            }
        }
    }

    private void DrawGameIconGrid(Shortcut shortcut)
    {
        for (var index = 0; index < GameIcons.Length; index++)
        {
            if (index % GridColumns != 0)
            {
                ImGui.SameLine();
            }

            var result = ImGuiHelpers.SeStringWrapped(GameIconTexts[index], new SeStringDrawParams(), new ImGuiId(index + 1), ImGuiButtonFlags.MouseButtonLeft);
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(GameIconNames[index]);
            }

            if (result.Clicked)
            {
                shortcut.IconKind = IconKind.GameIcon;
                shortcut.GameIcon = GameIcons[index];
                save();
            }
        }
    }

    private void DrawImagePicker(Shortcut shortcut)
    {
        ImGui.TextWrapped(shortcut.ImagePath.Length == 0 ? "No image picked yet." : shortcut.ImagePath);
        if (ImGui.Button("Browse"))
        {
            fileDialog.OpenFileDialog("Pick an icon image", ImageFilter, (picked, paths) =>
            {
                if (!picked || paths.Count == 0 || string.IsNullOrEmpty(paths[0]))
                {
                    return;
                }

                shortcut.ImagePath = paths[0];
                shortcut.IconKind = IconKind.Image;
                save();
            }, 1, null, false);
        }

        if (shortcut.ImagePath.Length > 0 && shortcut.IconKind != IconKind.Image && ImGui.Button("Use this image"))
        {
            shortcut.IconKind = IconKind.Image;
            save();
        }

        ImGui.TextDisabled("Square PNGs with a transparent background look best.");
        ImGui.TextDisabled("If the image can't be drawn, the flat icon shows instead.");
    }

    private string UniqueLabel(string wanted, Shortcut? self)
    {
        var baseLabel = wanted.Length == 0 ? "Shortcut" : wanted;
        var candidate = baseLabel;
        for (var suffix = 2; LabelTaken(candidate, self); suffix++)
        {
            candidate = $"{baseLabel} {suffix}";
        }

        return candidate;
    }

    private bool LabelTaken(string label, Shortcut? self)
    {
        foreach (var shortcut in configuration.Shortcuts)
        {
            if (shortcut != self && string.Equals(shortcut.Label, label, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeCommand(string command)
    {
        var trimmed = command.Trim();
        return trimmed.Length == 0 || trimmed[0] == '/' ? trimmed : "/" + trimmed;
    }

    private static SeIconChar[] BuildGlyphs()
    {
        var glyphs = new List<SeIconChar>();
        foreach (var glyph in Enum.GetValues<SeIconChar>())
        {
            var name = glyph.ToString();
            if (!name.StartsWith("Ime", StringComparison.Ordinal) && !name.StartsWith("AutoTranslate", StringComparison.Ordinal) && !glyphs.Contains(glyph))
            {
                glyphs.Add(glyph);
            }
        }

        return [.. glyphs];
    }

    private static BitmapFontIcon[] BuildGameIcons()
    {
        var icons = new List<BitmapFontIcon>();
        foreach (var icon in Enum.GetValues<BitmapFontIcon>())
        {
            if (icon != BitmapFontIcon.None && !icons.Contains(icon))
            {
                icons.Add(icon);
            }
        }

        return [.. icons];
    }
}
