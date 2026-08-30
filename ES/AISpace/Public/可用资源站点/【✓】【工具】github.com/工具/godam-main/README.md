# **godam**

![Rust 1.81](https://img.shields.io/badge/rust-1.81-orange)
[![Build Status](https://github.com/nilsiker/godam/actions/workflows/release.yml/badge.svg)](https://github.com/nilsiker/godam/actions/workflows/release.yml)

**godam** (**God**ot **A**sset **M**anager) is an unofficial, lightweight command-line tool designed to streamline the management of assets from the Godot Asset Library.

<p align="center">
  <img alt="an icon of a orange pixel-art robot head, with a command line icon obscuring its face, titled 'godam' just below it" src="media/godam.png" />
</p>

The goal of **godam** is simple: allow developers to easily manage Godot project assets via the command line, keeping asset dependencies out of version control systems (VCS). Inspired by package managers like Cargo and npm, **godam** uses a declarative configuration file to track assets, making them easily installable without cluttering your VCS.

> ⚠️ **godam** modifies files within your project directory, specifically under the `addons` folder. While care has been taken to avoid any mishaps, always back up your project, and understand you are using a tool that **creates**, **modifies** and **deletes** files on your local computer.

## ✨ Features

Here's a rundown of what **godam** can currently do:

- **Init** your Godot project for godam usage.
- **Search** the Godot Asset Library API for assets by ID.
- **Install** assets from the Godot Asset Library using their ID.
- **Uninstall** assets based on their ID.
- **List** all assets managed by **godam**.
- **Clean** the local asset cache, removing all downloaded zip archives.

<p align="center">
  <img alt="command-line showcase of initializing, searching and installing using godam" src="media/godam_basic_example.gif" />
</p>

## ❔ How It Works

**godam** sets up your Godot project by creating a `godam.toml` file, a `.godam` cache folder, and a `.gitignore` within your `addons` folder. The `.gitignore` ensures that only the `godam.toml` configuration file is tracked in Git, while addon files are omitted.

When you run `godam install <ID>`, the following happens:

1. Asset information is retrieved from the Godot Asset Library API and added to `godam.toml`.
2. The `.godam` cache is checked for the asset; if not cached, the asset zip is downloaded.
3. **godam** maps the asset's ID to its install location in `godam.toml`, keeping track of what plugin ID maps to what install folder.
4. The asset's `addons` folder is extracted from the zip and copied into your project.

This process is repeated for every asset listed in the `godam.toml` file.

> ⚠️ **godam** currently only respects addon folder structure, meaning that it expects to find the `addons` folder in either directly inside the zip file or located under an immediate folder in the archive. Any other folder structure will throw an error, and cannot be installed using godam.

## ⚙️ Quickstart

Let's walk through installing [LimboAI for Godot 4.3](https://godotengine.org/asset-library/asset/3228) with **godam**:

First off you need to install godam. You can download the [latest release binary](https://github.com/nilsiker/godam/releases/latest) and add it to your PATH, or build it from source using `cargo install godam`.

> ⚠️ When installing from source, the MSRV (Minimum Supported Rust Version) is 1.81.

Once installed, follow the procedure below to try it out:

1. Navigate to your Godot project directory:  
   `cd path/to/your/godot/project`

2. Initialize **godam** in your project:  
   `godam init`

3. Search for an asset by name:  
   `godam search LimboAI`

4. Install the asset using its ID:  
   `godam install 3228`

5. List all assets managed by **godam**:  
   `godam list`

6. Install all assets defined in `godam.toml`:  
   `godam install`

7. Clean the cache:  
   `godam clean`

## 🚧 Disclaimer

This is a tool designed to fit my workflow for managing Godot assets via the command line. **godam** is not an official Godot tool or product, so its functionality and scope are limited to my current use case. That said, it's open to improvement and feedback!

If you encounter any issues or have suggestions for improvements, feel free to open an issue or submit a pull request. **Contributions are more than welcome!**

## 🔮 Future Ideas

Here’s what’s on the roadmap for future development:

- Asset version freezing and upgrading (awaiting asset library API support)
- Nicer console output
- Global caching and symlinks
- Test suites for preventing bugs and regressions
- General codebase improvements, improving clarity, maintainability and performance

---

## 🤝 Contributing

Want to help make **godam** even better? I’d love to have your contributions! Whether it’s fixing a bug, adding new features, or suggesting improvements, every bit helps.

To get started:

1. Fork the repository and clone it to your machine.
2. Create a new branch for your feature or fix.
3. Submit a pull request when you’re ready!

Be sure to check out the issues tab for ideas on what needs work. Do not be a stranger, feel free to reach out, and let's build something cool together for the Godot community!

##

## License

This tool is licensed under the [MIT License](./LICENSE).
