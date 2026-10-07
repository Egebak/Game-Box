# Minigame registration contract

`MiniGameDefinition` holds the stable ID, visible title, entry scene path, icon texture path and short description. `MiniGameRegistry.CreateDefault()` registers the games available in this build. `MainMenu` enumerates that registry to construct launcher tiles and calls `ChangeSceneToFile` with the chosen scene path. The menu does not know any minigame classes.

To add a game:

1. Put its own entry scene and scripts under `Games/<Name>/`.
2. Add a `MiniGameDefinition` in `MiniGameRegistry.CreateDefault()`.
3. Keep game-specific state within that game's folder. Share only reusable UI or app state through `Shared/` and `Core/`.
4. Offer a path back to `res://Core/MainMenu.tscn`; clear scene pause before changing scenes.

`GameBoxState` saves Tank Arena and Gravemaskine-plads completion flags to `user://game_box_state.json`. The registry is the extension point; no broad minigame inheritance hierarchy is required.
