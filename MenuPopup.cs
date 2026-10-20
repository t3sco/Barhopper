using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace Barhopper;

public sealed unsafe class MenuPopup
{
    public const uint WholeMenu = 0;
    private const string PopupId = "###BarhopperMenu";
    private const long ReopenGuardMilliseconds = 250;
    private const float AnchorGap = 4f;

    private readonly MainMenuData menu;
    private readonly IFramework framework;
    private bool pending;
    private bool open;
    private uint target;
    private Vector2 anchorMin;
    private Vector2 anchorMax;
    private uint closedTarget;
    private long closedAt;

    public MenuPopup(MainMenuData menu, IFramework framework)
    {
        this.menu = menu;
        this.framework = framework;
    }

    public bool IsOpen => open || pending;

    public void Toggle(uint categoryId, (Vector2 Min, Vector2 Max) bounds)
    {
        if (closedTarget == categoryId && Environment.TickCount64 - closedAt < ReopenGuardMilliseconds)
        {
            return;
        }

        target = categoryId;
        (anchorMin, anchorMax) = bounds;
        pending = true;
    }

    public void Draw()
    {
        if (pending)
        {
            pending = false;
            open = true;
            ImGui.OpenPopup(PopupId);
        }

        if (!open)
        {
            return;
        }

        PlaceNextWindow();
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.PopupRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8f, 6f));
        var visible = ImGui.BeginPopup(PopupId);
        if (visible)
        {
            DrawContents();
            ImGui.EndPopup();
        }

        ImGui.PopStyleVar(3);
        if (!visible)
        {
            open = false;
            closedTarget = target;
            closedAt = Environment.TickCount64;
        }
    }

    private void DrawContents()
    {
        if (target != WholeMenu)
        {
            var category = menu.Find(target);
            if (category != null)
            {
                DrawCommands(category);
            }

            return;
        }

        foreach (var category in menu.Categories)
        {
            if (ImGui.BeginMenu(category.Name))
            {
                DrawCommands(category);
                ImGui.EndMenu();
            }
        }
    }

    private void DrawCommands(MenuCategory category)
    {
        var uiModule = UIModule.Instance();
        foreach (var command in category.Commands)
        {
            if (uiModule != null && !uiModule->IsMainCommandUnlocked(command.Id))
            {
                continue;
            }

            ImGui.PushID((int)command.Id);
            var clicked = ImGui.Selectable(command.Name);
            ImGui.PopID();
            if (clicked)
            {
                var id = command.Id;
                _ = framework.RunOnFrameworkThread(() => Execute(id));
                ImGui.CloseCurrentPopup();
            }
        }
    }

    private void PlaceNextWindow()
    {
        var viewport = ImGui.GetMainViewport();
        var below = anchorMax.Y < viewport.Pos.Y + (viewport.Size.Y * 0.5f);
        var rightSide = anchorMin.X > viewport.Pos.X + (viewport.Size.X * 0.5f);
        var position = new Vector2(
            rightSide ? anchorMax.X : anchorMin.X,
            below ? anchorMax.Y + AnchorGap : anchorMin.Y - AnchorGap);
        var pivot = new Vector2(rightSide ? 1f : 0f, below ? 0f : 1f);
        ImGui.SetNextWindowPos(position, ImGuiCond.Always, pivot);
    }

    private static void Execute(uint commandId)
    {
        var uiModule = UIModule.Instance();
        if (uiModule != null)
        {
            uiModule->ExecuteMainCommand(commandId);
        }
    }
}
