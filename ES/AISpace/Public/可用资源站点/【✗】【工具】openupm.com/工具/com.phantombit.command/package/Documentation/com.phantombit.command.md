# .Command Documentation

Thank you for using **.Command**, the Unity Command Line Tool and runtime log viewer.

## Overview

.Command allows you to:

- View your Unity application logs **in-game** on any platform, without digging through log files.
- Search and filter logs with ease.
- Inspect Unity GameObjects and all their components, fields, and properties at runtime.
- Email logs directly from your device.
- Execute arbitrary C# code and properties interactively using simple annotations.

## Getting Started

### Install via UPM
1. Add a new entry to your manifests.json
`"com.phantombit.command": "https://github.com/ArtOfSettling/UnityInGameConsole.git?path=unity/Packages/com.phantombit.command#v1.0.0"`
2. Drag the `Packages/com.phantombit.command/Runtime/Platform/Prefabs/DebugConsoleLoader.prefab` into any scene and you are done!

For detailed usage instructions, setup guides, and advanced features, please visit our full documentation hosted on ReadTheDocs:

[https://dotcommand-documentation.readthedocs.io/en/latest/](https://dotcommand-documentation.readthedocs.io/en/latest/)

### Next Steps
Read more on how to use .command
1. [Programmatic Instantiation](https://dotcommand-documentation.readthedocs.io/en/latest/learn/step_by_step/quick_start.html)
2. [Exposing Custom Commands](https://dotcommand-documentation.readthedocs.io/en/latest/learn/step_by_step/custom_commands.html#introduction)
3. [Adding Custom Log Levels](https://dotcommand-documentation.readthedocs.io/en/latest/learn/step_by_step/logs_and_filters.html#introduction)
4. [Built in commands](https://dotcommand-documentation.readthedocs.io/en/latest/learn/step_by_step/built_in_commands.html)

## Basic Usage Tips

- Use the console by toggling visibility (default key binding is `` ` ``).
- Customize your theme and settings in the `.Command` settings menu.
- Annotate your C# methods and properties with `[Command]` to expose them in the UI.
- Use the log viewer to filter by log level or search text.
- Email logs directly when needed to aid debugging.

## Troubleshooting

- If you switch to the new Unity Input System, ensure your assembly definitions include references to `com.unity.inputsystem`.
- For issues building or importing the package, verify you have the correct Unity version and dependencies installed.

## Feedback & Contributions

We welcome your feedback and contributions!
Visit our GitHub repo for issues, feature requests, and pull requests:
[https://github.com/ArtOfSettling/dotCommand](https://github.com/ArtOfSettling/dotCommand)

---

*Happy debugging!*
