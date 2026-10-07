# Game Box

A small game collection for a child, built with **Godot 4.6.3 .NET** and C#. The launcher offers **Tank Arena**, **Gravemaskine-plads**, and **Ordmission**.

On the first launch, Game Box asks for the child's name. It saves the name locally with game progress in `user://game_box_state.json` and greets the child on later launches without asking again. The name is never sent to a server.

## Run

1. Open `project.godot` in the Godot 4.6.3 .NET editor.
2. Build the C# solution in Godot, or run `dotnet build GameBox.csproj`.
3. Press F6/F5 in Godot to play the current/main scene. The main scene is the Game Box launcher.

The desktop viewport is designed at 1920×1080 and opens in a 1280×720 window. UI uses containers and stretches with the window.

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

## Structure

- `Core/`: launcher, registry and small persistent state saved under Godot's `user://` path.
- `Games/TankArena/`: the independent Tank Arena entry scene and gameplay scripts.
- `Games/ConstructionSite/`: the independent construction mission, excavator, soil pile, truck and effects.
- `Games/WordMission/`: Ordmission's separate learning data, adaptive session generator, activities, parent controls, and optional recording hooks.
- `Shared/UI/`: visual controls used by the launcher and game.
- `Shared/Audio/`: short original WAV effects for menus, engines, combat, construction interactions, warnings, and results.
- `Tests/`: dependency-free console checks for plain C# game and app logic.

See `MINIGAME_DESIGN.md` before adding another game. The launcher reads registry metadata and does not reference Tank Arena classes.

## Ordmission

Choose **Ordmission** from Game Box and press **Start mission**. A short session mixes **Find lyden**, **Byg ordet**, **Hvilket ord?**, **Ordporten**, and **Tankmission**. Tap or click the large choices. **Esc** pauses, and the pause menu returns to Game Box. The **Min vej** screen shows a simple mission path; **For voksne** shows learning details, sound and letter-case settings, and a learning reset. In debug builds, **F9** opens content and stage controls.

The starter data is in `Games/WordMission/Data/starter.json`. Its five example words are **IS, NU, SOL, MUS, HUS**. All five are marked `needs-review`. Letter names, sound cues, sound-to-word relationships, and the pictured meaning of **NU** also need review. No human-recorded phonics audio is included yet. Missing recordings leave the game playable in a clearly labeled visual test mode and produce Godot warnings. **Educational content and Danish phonics recordings must be reviewed by a Danish-speaking adult before relying on the game for reading instruction.** See `Games/WordMission/ORDMISSION_DESIGN.md` for the data schema, review workflow, and recording paths.

## Tests

Run `dotnet run --project Tests/GameBox.Tests.csproj`.

For engine-level flow checks, run `Tests/LauncherSmoke.tscn`, `Tests/NamePromptSmoke.tscn`, `Tests/WordMissionSmoke.tscn`, `Tests/TankArenaSmoke.tscn` and `Tests/ConstructionSmoke.tscn` with Godot's `--headless` option. They quit with a nonzero code on failure. The Ordmission check completes all nine activities, exercises assistance, and reopens saved progress. The construction check performs the complete eight-load mission and verifies truck departure, pause, free play and saved completion.

## Current limitations

- Primitive meshes and synthesized arcade effects are intentional first-pass presentation. Destroyed enemy tanks now burst into sparks, metal pieces and smoke before their temporary wrecks disappear.
- Enemy obstacle steering uses local rays rather than full navigation; it can occasionally choose an awkward route.
- Enemy shells can hit other enemy tanks. All enemy destruction counts toward the ten-tank objective.
- Desktop keyboard and mouse only. No touch controls yet.
- Construction vehicles use simple scripted movement and primitive meshes. Only the excavator is playable in this version; other construction machines and new missions are future additions.
