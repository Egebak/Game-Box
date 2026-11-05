# Game Box

A small game collection for a child, built with **Godot 4.6.3 .NET** and C#. The launcher offers **Tank Arena**, **Gravemaskine-plads**, **Ordmission**, and **Space Duel**.

On the first launch, Game Box asks for the child's name. It saves the name locally with game progress in `user://game_box_state.json` and greets the child on later launches without asking again. The name is never sent to a server.

## Run

1. Install the **Godot 4.6.3 .NET editor** (the regular Godot editor does not support C#) and the **.NET 8 SDK**.
2. Clone the repository and open `project.godot` in that editor. Allow the first asset import to finish.
3. Build C# with the Build button in Godot, or run `dotnet restore GameBox.sln` and `dotnet build GameBox.sln` in the project folder.
4. Press F5 to start the Game Box launcher.

If Godot says **“Load failed due to missing dependencies”** on a fresh clone, check that you opened the .NET editor and completed the C# build before running. The generated `.godot/` import cache and compiled assemblies are intentionally not in Git; Godot and the .NET SDK recreate them locally. If the build fails, check the Godot Output/MSBuild panel or run `dotnet build GameBox.sln` to see the underlying error.

## Windows standalone build

Install the matching **Godot 4.6.3 .NET export templates** in the editor, then choose **Project → Export → Windows Desktop**. The checked-in `export_presets.cfg` includes the JSON learning data. For a command-line release export, run:

```powershell
godot --headless --path . --export-release "Windows Desktop" "build/windows/Game Box.exe"
```

Distribute the **entire** `build/windows` folder: `Game Box.exe`, `Game Box.pck`, and `data_GameBox_windows_x86_64/` must stay together. The exported game includes its .NET runtime, so players do not need the Godot editor or .NET SDK.

The desktop viewport is designed at 1920×1080 and opens maximized, with a 1600×900 window size when restored. The canvas renders directly at the display's resolution while UI containers adapt to the available space. The 3D games render at 125% of the window resolution before downsampling for sharper detail; Space Duel's vector shapes use smoothed edges.

## Tank Arena controls

| Action | Control |
| --- | --- |
| Drive forward/back | W / S |
| Turn hull | A / D |
| Aim turret | Mouse |
| Fire | Left mouse button |
| Pause / resume | Esc |

The pause screen provides resume, restart and return to the Game Box. Destroy ten normal tanks to summon the boss; its twin cannons fire two shells together. Every destroyed tank leaves a smoking wreck. When the boss is destroyed, play continues for five seconds before the victory screen appears. Defeat and victory screens have replay and return buttons.

The player has three shell charges. Shots can be fired 0.32 seconds apart; each charge takes 1.7 seconds to recover. At most three player shells can be in flight at once. The HUD shows ready charges and recharge progress.

## Gravemaskine-plads

Choose **Opgave: Fyld lastbilen** or **Fri leg** from the game's opening menu. In the mission, move eight bucket loads from the brown pile to the truck. The pile visibly shrinks and the truck bed fills one mound at a time. The source pile renews when empty, so ground dumps cannot make the mission impossible. Once full, the truck drives away and **Færdig!** appears. Completing the mission is recorded in the same `user://game_box_state.json` file used by Tank Arena. Free play has no end screen.

| Action | Control |
| --- | --- |
| Drive forward/back | W / S |
| Turn tracks | A / D |
| Aim rotating upper body | Mouse |
| Scoop near highlighted soil / dump when the loaded bucket is over the truck bed | Left click |
| Empty a full bucket onto the ground | Right click |
| Pause / resume | Esc |

Move close to the glowing work area before clicking. A large red circle marks the truck while the bucket holds soil; the bed turns green when the bucket is over it and ready to unload. The arm and bucket animate automatically; there are no separate hydraulic controls. The pause and success screens offer restart and return to the Game Box.

## Space Duel

Choose **Solo mod computer** or **To spillere** from Space Duel's own menu. Solo randomly selects one of three simple AI behaviors. Each ship has eight health points and a rechargeable energy bar shared by movement and weapons. Momentum carries the ship after thrust stops; braking is half as strong as acceleration. Ships, lasers, and torpedoes wrap around all four arena edges. Ship collisions make a small burst and send both ships recoiling and spinning. Lasers deal one damage, while guided torpedoes deal four. A laser that hits any torpedo detonates it. Once launched, either weapon can hit either ship, including its owner; a self-destruction awards the point to the other player. Torpedoes steer while they have fuel, then coast briefly and explode.

| Action | Player 1 | Player 2 |
| --- | --- | --- |
| Accelerate / brake | W / S | Up / Down |
| Rotate | A / D | Left / Right |
| Fire laser | Space | Enter or numpad 0 |
| Fire torpedo | Shift | + or numpad + |
| Pause / resume | Esc | Esc |

The first ship to ten kills wins. After a short finish sequence, the victory screen shows the winning ship and its shot, hit, speed, and energy statistics. Press **Enter** there to return to Space Duel's menu. The pause menu also offers a direct return to Game Box.

## Structure

- `Core/`: launcher, registry and small persistent state saved under Godot's `user://` path.
- `Games/TankArena/`: the independent Tank Arena entry scene and gameplay scripts.
- `Games/ConstructionSite/`: the independent construction mission, excavator, soil pile, truck and effects.
- `Games/WordMission/`: Ordmission's separate learning data, adaptive session generator, activities, parent controls, and optional recording hooks.
- `Games/SpaceDuel/`: the space duel scene, controls, AI, movement, weapons, and victory flow.
- `Shared/UI/`: visual controls used by the launcher and game.
- `Shared/Audio/`: short original WAV effects for menus, engines, combat, construction interactions, warnings, and results.
- `Tests/`: dependency-free console checks for plain C# game and app logic.

See `MINIGAME_DESIGN.md` before adding another game. The launcher reads registry metadata and does not reference Tank Arena classes.

## Ordmission

Choose **Ordmission** from Game Box and press **Start mission**. A short session mixes **Find lyden**, **Byg ordet**, **Hvilket ord?**, **Ordporten**, and **Tankmission**. Tap or click the large choices. **Esc** pauses, and the pause menu returns to Game Box. The **Min vej** screen shows a simple mission path; **For voksne** shows learning details, sound and letter-case settings, and a learning reset. In debug builds, **F9** opens content and stage controls.

The starter data is in `Games/WordMission/Data/starter.json`. Its five example words are **IS, NU, SOL, MUS, HUS**. All five are marked `needs-review`. Letter names, sound cues, sound-to-word relationships, and the pictured meaning of **NU** also need review. No human-recorded phonics audio is included yet. Missing recordings leave the game playable in a clearly labeled visual test mode and produce Godot warnings. **Educational content and Danish phonics recordings must be reviewed by a Danish-speaking adult before relying on the game for reading instruction.** See `Games/WordMission/ORDMISSION_DESIGN.md` for the data schema, review workflow, and recording paths.

## Tests

Run `dotnet run --project Tests/GameBox.Tests.csproj`.

For engine-level flow checks, run `Tests/LauncherSmoke.tscn`, `Tests/NamePromptSmoke.tscn`, `Tests/WordMissionSmoke.tscn`, `Tests/TankArenaSmoke.tscn`, `Tests/ConstructionSmoke.tscn`, and `Tests/SpaceDuelSmoke.tscn` with Godot's `--headless` option. They quit with a nonzero code on failure. The Ordmission check completes all nine activities, exercises assistance, and reopens saved progress. The construction check performs the complete eight-load mission and verifies truck departure, pause, free play and saved completion. The Space Duel check verifies both modes, movement and energy, weapons, AI, match finish, and the return to its own menu.

## Current limitations

- Primitive meshes and synthesized arcade effects are intentional first-pass presentation. Destroyed enemy tanks now burst into sparks, metal pieces and smoke before their temporary wrecks disappear.
- Enemy obstacle steering uses local rays rather than full navigation; it can occasionally choose an awkward route.
- Enemy shells can hit other enemy tanks. All enemy destruction counts toward the ten-tank objective.
- Desktop keyboard and mouse only. No touch controls yet.
- Construction vehicles use simple scripted movement and primitive meshes. Only the excavator is playable in this version; other construction machines and new missions are future additions.
