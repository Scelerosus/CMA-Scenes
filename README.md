# CMA Scenes

A BepInEx (IL2CPP) plugin for *Corrupted Modeling Agency* 0.66 (adult game, 18+). It is the successor of CMA Shake:
a choice of places for the sex (furniture found in the room), natural arms, a plugin-made condom and an X-ray view.

**Status: 0.3.25, experimental.** See `MODLOG.md` for the change history and known limitations.

## Build
Needs the game with BepInEx 6 (IL2CPP) installed and run once (so that `BepInEx\interop` exists).

    dotnet build CMAScenes.csproj -c Release -p:GameDir="C:\path\to\Corrupted Modeling Agency 0.66"

The DLL is copied to `BepInEx\plugins\CMAScenes` (add `-p:SkipInstall=true` to only build).

## Use
`L` starts the sex, `H` twerk, `J` opens the settings window. It is incompatible with CMA Shake (disable that plugin).

No game files are included in this repository.
