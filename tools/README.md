# Workspace tools

Place future standalone tool projects here and add them to `Game.sln` with `dotnet sln Game.sln add tools/<ToolName>/<ToolName>.csproj`.

CharacterEditor is the dedicated visual authoring app at `src/CharacterEditor/`; it belongs in the solution's `src` tree. Future standalone tools that need the static GLB importer can reference `src/Game.Assets/Game.Assets.csproj`. The existing Blender asset generation and validation scripts remain with their source assets under `assets/villager/`.
