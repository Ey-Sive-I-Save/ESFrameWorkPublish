# .Command

![Unity Command Line Tool](https://dotcommand-documentation.readthedocs.io/en/latest/_images/suggestions.svg)

A powerful runtime command console and in-game inspector for Unity. It works across all platforms and brings advanced debugging and tooling features directly into your live game builds.

## Key Features

- **In-Game Log Viewer**  
  View and search your Unity logs while the game is running, without digging into log files — works in Editor and Player builds.

- **Runtime GameObject Inspector**  
  Explore active GameObjects and their components, fields, and properties live on any platform — including mobile and console.

- **Smart Commands & Auto-Complete**  
  Annotate C# methods and properties with attributes to generate interactive console buttons. Supports autocomplete, nested paths, and parameterized calls.

- **Email Logs from Device**  
  Instantly export and email logs or callstacks from any platform — perfect for remote testing and QA workflows.

---

## Getting Started

### Install via UPM
1. Add a new entry to your manifests.json
`"com.phantombit.command": "https://github.com/ArtOfSettling/UnityInGameConsoleUPM.git#v0.1.1"`
2. Drag the `Packages/com.phantombit.command/Runtime/Platform/Prefabs/DebugConsoleLoader.prefab` into any scene and you are done!

### Install via unity package
1. Grab the latest release from the [GitHub Releases Page](https://github.com/ArtOfSettling/dotCommand/releases).
2. Import the unity package into your unity project.
3. Drag the `WellFired/WellFired.Command/Platform/Prefabs/DebugConsoleLoader.prefab` into any scene and you are done!

### Next Steps
Read more on how to use .command
1. [Programmatic Instantiation](https://dotcommand-documentation.readthedocs.io/en/latest/learn/step_by_step/quick_start.html)
2. [Exposing Custom Commands](https://dotcommand-documentation.readthedocs.io/en/latest/learn/step_by_step/custom_commands.html#introduction)
3. [Adding Custom Log Levels](https://dotcommand-documentation.readthedocs.io/en/latest/learn/step_by_step/logs_and_filters.html#introduction)
4. [Built in commands](https://dotcommand-documentation.readthedocs.io/en/latest/learn/step_by_step/built_in_commands.html)

---

## Documentation

Full usage docs and API reference are available at:  
📖 [https://dotcommand-documentation.readthedocs.io/en/latest/](https://dotcommand-documentation.readthedocs.io/en/latest/)
