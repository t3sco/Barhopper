# Barhopper

Hop into any plugin from the server info bar.

Barhopper pins plugins and chat commands to the server info bar (the strip in the top right with the clock and world name) as small icons.

- **Link any plugin:** click to open its main window, its settings, or run a chat command.
- **Right-click** a linked icon to open that plugin's settings.
- **Pick an icon** from the game's flat glyphs or coloured icons, and recolour the text and glow.
- **Custom images (experimental):** use your own PNGs. The bar can only show game icons, so these are drawn on top of it.
- **Bar opacity and auto-hide:** dim the whole server info bar, or hide it until the cursor comes near.
- Shortcuts hide themselves while their plugin isn't loaded.

Penumbra and Glamourer are set up by default. Open the settings with `/barhopper` or from the plugin installer. Reorder or hide entries in Dalamud's own Server Info Bar settings.

## Install

1. In game, open `/xlsettings` and go to **Experimental**.
2. Under **Custom Plugin Repositories**, paste this URL, tick **Enabled** and click **Save**:

   ```
   https://raw.githubusercontent.com/t3sco/Barhopper/main/repo.json
   ```

3. Open `/xlplugins`, search for **Barhopper** and install it.

## Releasing

1. Bump `<Version>` in `Barhopper.csproj`.
2. Run `powershell -ExecutionPolicy Bypass -File .\release.ps1`. It builds in Release and rewrites `repo.json` for that version.
3. Create a GitHub release tagged `v<version>` (for example `v1.0.0.0`) and attach `bin\Release\Barhopper\latest.zip`.
4. Commit and push `repo.json`. Installed copies update on the next Dalamud check.

## Building

Needs the .NET 10 SDK and Dalamud installed through XIVLauncher.

```
dotnet build Barhopper.csproj -c Release
```
