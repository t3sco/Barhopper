using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;

namespace Barhopper;

public sealed class MainMenuData
{
    public MainMenuData(IDataManager data)
    {
        var commandSheet = data.GetExcelSheet<MainCommand>();
        var categories = new List<MenuCategory>();
        foreach (var category in data.GetExcelSheet<MainCommandCategory>())
        {
            var name = category.Name.ExtractText();
            if (name.Length == 0)
            {
                continue;
            }

            var commands = new List<MenuCommand>();
            foreach (var command in commandSheet)
            {
                var commandName = command.Name.ExtractText();
                if (command.MainCommandCategory.RowId == category.RowId && commandName.Length > 0)
                {
                    commands.Add(new MenuCommand(command.RowId, commandName, command.SortID));
                }
            }

            if (commands.Count == 0)
            {
                continue;
            }

            commands.Sort(static (left, right) => left.SortOrder.CompareTo(right.SortOrder));
            categories.Add(new MenuCategory(category.RowId, name, [.. commands]));
        }

        Categories = [.. categories];
    }

    public MenuCategory[] Categories { get; }

    public MenuCategory? Find(uint id)
    {
        foreach (var category in Categories)
        {
            if (category.Id == id)
            {
                return category;
            }
        }

        return null;
    }
}

public sealed record MenuCategory(uint Id, string Name, MenuCommand[] Commands);

public sealed record MenuCommand(uint Id, string Name, int SortOrder);
