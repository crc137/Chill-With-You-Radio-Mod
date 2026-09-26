<div align="center">
  <a href="https://github.com/coonlink">
    <img width="90px" src="logo.png" alt="Logo" />
  </a>
  <h1>Chill with You : Lo-Fi Story — Radio Mod</h1>

[![English](https://img.shields.io/badge/lang-English%20🇺🇸-white)](README.md)
[![Русский](https://img.shields.io/badge/язык-Русский%20🇷🇺-white)](README.ru.md)

<img alt="last-commit" src="https://img.shields.io/github/last-commit/crc137/Chill-With-You-Radio-Mod?style=flat&amp;logo=git&amp;logoColor=white&amp;color=0080ff" style="margin: 0px 2px;">
<img alt="repo-top-language" src="https://img.shields.io/github/languages/top/crc137/Chill-With-You-Radio-Mod?style=flat&amp;color=0080ff" style="margin: 0px 2px;">
<img alt="repo-language-count" src="https://img.shields.io/github/languages/count/crc137/Chill-With-You-Radio-Mod?style=flat&amp;color=0080ff" style="margin: 0px 2px;">
<img alt="version" src="https://img.shields.io/badge/version-26.1.2-blue" style="margin: 0px 2px;">
</div>

<br />

<div align="center">
  <p>Adds working <b>internet radio</b> to the game. Station switching works without stuttering.</p>
</div>

## Requirements

For the radio to work you need **all** of these:

1. The game **Chill with You : Lo-Fi Story** (any version, Steam), launched once.
2. **BepInEx 5.x** in the game folder → `...Chill with You Lo-Fi Story/BepInEx/`
   (the installer installs it automatically if it's missing, e.g. after a Steam reinstall).
3. The plugin files `RadioStreamPlugin.dll` and `NLayer.dll` (this mod).
4. Internet connection (radio streams are live online).


## How to install (player, no build needed)

**Option A — one-click installer (recommended)**

Put `install.sh`, `install.bat`, `RadioStreamPlugin.dll`, `NLayer.dll` and `radiostations.txt` in one folder and run the installer for your OS:

- **Windows:** double-click `install.bat`
- **Linux / Steam Deck:** `./install.sh`

The installer finds the game in **any Steam library** (including non-default and external drives), installs BepInEx automatically if it's missing (e.g. after you deleted and re-downloaded the game), and copies the mod into `BepInEx/plugins`. If the game can't be found it will ask you for the path manually.

**Option B — manual**

1. Install the game via Steam and launch it **once** so folders are created.
2. Put **BepInEx 5.x** into the game folder:
   - Windows: `...Chill with You Lo-Fi Story/BepInEx/`
   - Steam Deck / Linux (Flatpak):
     `~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/Chill with You Lo-Fi Story/BepInEx/`
3. Copy **all files** from this repo into the **plugins** folder:
   ```
   BepInEx/plugins/RadioStreamPlugin.dll     ← the mod
   BepInEx/plugins/NLayer.dll                ← MP3 decoder (required)
   BepInEx/plugins/radiostations.txt         ← station list
   ```
4. Launch the game. In the music menu, switch stations with **J / K**.

If the station list is missing, the plugin uses a default one. If BepInEx is missing after a reinstall, run `install.sh` / `install.bat` — it will install BepInEx for you.



## How to configure stations

`radiostations.txt` — one station per line, format (URL first, separated by `|`):

```
URL|Name|Author|Description
```

Example:

```
http://example.com/lofi.mp3|Lo-Fi Beats
http://example.com/jazz.pls|Jazz Radio
```

Edit the file, then **restart the game** for changes to apply. Stations play only if the URL is reachable (some `.pls`/`.m3u` links need the browser first — prefer direct `.mp3`/`.aac` links).



## Controls

| Key | Action |
|---|---|
| **J** | Previous station |
| **K** | Next station |



## Troubleshooting

- **No radio in menu / no music** → BepInEx is not installed or the DLL is not in `BepInEx/plugins`. Make sure `BepInEx/core` exists **and** `NLayer.dll` is next to `RadioStreamPlugin.dll`. If the game was just reinstalled, run the installer again — it restores BepInEx and the plugin.
- **Station doesn't play** → URL unreachable or unsupported format. Replace it in `radiostations.txt` with a direct stream link.
- **No BepInEx console** → add `[Logging.Console] Enabled = true` in `BepInEx/config/BepInEx.cfg`.



## Build from source (developers)

Requires .NET SDK (>= 6) and `build.sh` (finds the game on Linux / Windows automatically, compiles and copies the DLL). Run:

```bash
./build.sh
```
