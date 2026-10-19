# BetterFreeCam

An instant-response free camera for **PEAK**. Move and turn immediately; release the controls and the camera stops. The native cinematic camera's acceleration, drag, shake and depth-of-field path are bypassed while freecam is active.

**Derived from [Bananegame FreeCAM 0.0.2](https://thunderstore.io/c/peak/p/Bananegame/FreeCAM/).** This repository is a GitHub fork of [xBananegame/PEAKModes](https://github.com/xBananegame/PEAKModes), which did not contain FreeCAM's plugin source when this implementation was created. BetterFreeCam is an independently written source reimplementation, not a modification of an available upstream FreeCAM source tree. See [Credits and provenance](#credits-and-provenance).

## Install

1. Install [BepInExPack for PEAK](https://thunderstore.io/c/peak/p/BepInEx/BepInExPack_PEAK/).
2. **Disable or uninstall Bananegame FreeCAM and any earlier local FreeCAM fix before enabling BetterFreeCam.** Both use F3 and control the same camera.
3. Install BetterFreeCam with your mod manager and launch the modded game.

For a manual install, copy the package's `BepInEx/plugins/BetterFreeCam/BetterFreeCam.dll` into the same location under your modded game or profile directory.

This is a client-side camera mod. Other players do not need it. While it is active, movement input for your character is suppressed; the character remains in the game world and is still subject to normal game physics and hazards.

## Controls

| Key | Action |
| --- | --- |
| F3 | Enter or leave freecam |
| Escape | Leave freecam |
| W / A / S / D | Move relative to the camera |
| Space / Left Ctrl | Move up / down |
| Left Shift | Move 3 times faster, immediately |
| Mouse | Look around, without inertia |
| Mouse wheel | Adjust field of view, without smoothing |
| H | Toggle the HUD |

Available in the Airport and gameplay scenes. Your original camera, player-input, HUD, cursor and field-of-view states are restored on exit, scene changes or plugin disable. The camera has no collision; the mod does not teleport your character.

## Settings and migration

After the first launch, edit `BepInEx/config/com.github.LeonYewLey.BetterFreeCam.cfg` while the game is closed:

```ini
[Controls]
FreeCamHotkey = F3

[FreeCam Settings]
MoveSpeed = 0.05
MouseSensitivity = 0.05
```

- `MoveSpeed = 0.05` means 3 metres per second; Shift makes it 9 metres per second.
- `MouseSensitivity = 0.05` means 0.5 degrees per raw mouse-axis unit.

BetterFreeCam uses its own plugin GUID and config file. To migrate preferences, copy the three values above from your old `com.Bananegame.FreeCAM.cfg` into the new file. The default numeric values are compatible with the tested local no-inertia build. The original FreeCAM 0.0.2 used the game's native camera for visible movement, so its effective speed and sensitivity were not reliably controlled by these settings.

## Compatibility

The native `MainCameraMovement` component is paused during freecam, and its original enabled state is restored afterwards. Camera mods attached to that component, including PerspectiveSwitcher, resume their normal behavior when freecam exits. Mods that independently force the camera transform can still interfere; do not run two freecam mods together.

The underlying no-inertia implementation was built and manually tested in PEAK before this public rename. The 1.0.0 rename changes the identity and packaging, not the camera behavior. Game updates that rename the internal `isGodCam` field or `ResetInput` method may require an update.

## Build and package

Requires a .NET SDK supporting `netstandard2.1`, your installed PEAK managed assemblies, and BepInEx 5. No game or loader assemblies are redistributed in this repository.

```powershell
dotnet build .\BetterFreeCam.csproj -c Release `
  -p:GameDir="<PEAK installation directory>" `
  -p:BepInExCorePath="<BepInEx core directory>" `
  --ignore-failed-sources
```

Or build and produce a ready-to-upload Thunderstore ZIP:

```powershell
.\scripts\package.ps1 `
  -GameDir "<PEAK installation directory>" `
  -BepInExCorePath "<BepInEx core directory>"
```

The DLL is written to `bin/Release/netstandard2.1/BetterFreeCam.dll`; the package is `dist/BetterFreeCam-1.0.0.zip`. The package script checks metadata, icon dimensions and ZIP layout, and includes only the mod DLL, metadata, documentation and license. It does not require game files to be committed. There is no hosted build workflow because compilation needs local game assemblies.

## Credits and provenance

- Original mod concept and behavior: **Bananegame**, [FreeCAM 0.0.2](https://thunderstore.io/c/peak/p/Bananegame/FreeCAM/).
- Upstream repository: **xBananegame**, [PEAKModes](https://github.com/xBananegame/PEAKModes). Its MIT license and copyright notice are retained.
- BetterFreeCam implementation and maintenance: **LeonYew-Ley**.
- The upstream repository had no usable FreeCAM plugin source when this implementation was created. The installed FreeCAM 0.0.2 DLL and the local game's managed IL were inspected to understand the camera and input flow; this repository contains the resulting independently written replacement, not an exported upstream source tree or game code.
- The original DLL moved the plugin object's transform while enabling the game's `GodCam` path. The visible camera therefore still used the game's accumulated movement/look velocity and damping. BetterFreeCam controls the actual main camera directly and uses the game's input-reset path without running its cinematic camera update.
- The FreeCAM package's `LICENSE` file was an unselected-license placeholder. The [MIT license](LICENSE) in this fork covers the upstream repository contents already under MIT and the newly written BetterFreeCam implementation, documentation, scripts and original icon. It does not relicense the original FreeCAM package or PEAK. No original mod, game or BepInEx binaries are bundled.

Report problems and find releases at [LeonYew-Ley/BetterFreeCam](https://github.com/LeonYew-Ley/BetterFreeCam).
