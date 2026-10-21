using System.Text;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace Barhopper;

public static class WhatsNew
{
    private static readonly TimeSpan ToastDuration = TimeSpan.FromSeconds(20);

    private static readonly (Version Version, string Notes)[] Releases =
    [
        (new Version(1, 3, 0, 0),
            "• A short note like this now shows once after each update.\n"
            + "• Missed it? 1.2 can put the game's main menu on the bar: /barhopper, then \"Add the game menu to the bar\"."),
    ];

    public static void ShowIfUpdated(IDalamudPluginInterface pluginInterface, INotificationManager notifications, Configuration configuration, bool freshInstall)
    {
        var current = pluginInterface.Manifest.AssemblyVersion;
        var hasLastSeen = Version.TryParse(configuration.LastSeenVersion, out var lastSeen);
        if (hasLastSeen && lastSeen == current)
        {
            return;
        }

        configuration.LastSeenVersion = current.ToString();
        pluginInterface.SavePluginConfig(configuration);
        if (freshInstall)
        {
            return;
        }

        var notes = new StringBuilder();
        foreach (var (version, releaseNotes) in Releases)
        {
            var isNew = hasLastSeen ? version > lastSeen! && version <= current : version == current;
            if (isNew)
            {
                notes.AppendLine(releaseNotes);
            }
        }

        if (notes.Length == 0)
        {
            return;
        }

        notifications.AddNotification(new Notification
        {
            Title = $"Barhopper updated to {current.ToString(3)}",
            Content = notes.ToString().TrimEnd(),
            Type = NotificationType.Info,
            InitialDuration = ToastDuration,
        });
    }
}
