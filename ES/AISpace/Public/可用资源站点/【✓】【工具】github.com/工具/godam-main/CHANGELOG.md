# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- ...

### Changed

- ...

### Removed

- ...

## [0.1.2] - 2025-03-19

### Changed

- Add a handful of file system tests to ensure godam only operates inside the project directory.
- Bumped Dockerfile to use v4.4 of [Barichello's fantastic godot-ci image](https://github.com/abarichello/godot-ci). (https://github.com/nilsiker/godam/issues/24)
- Updated dependencies to latest versions.

## [0.1.1] - 2024-09-27

### Changed

- Config file elements are now kept sorted, to avoid file changes dependent on order of installation. (https://github.com/nilsiker/godam/issues/19)

## [0.1.0] - 2024-09-27

### Added

- First godam release 🥳
- **godam init**: Initialize Godot projects to use godam as your asset manager.
- **godam search {name}**: Search the Godot Asset Library for addons IDs based on {name}.
- **godam install {id}**: Install a addons based on their {id}.
- **godam uninstall {id}**: Uninstall a addons based on their {id}. Omit the id to uninstall all managed addons
- **godam list**: List all addons managed by godam.
- **godam clean**: Clean the zip cache from downloaded addons.

[unreleased]: https://github.com/nilsiker/godam/compare/v0.1.0...HEAD
[0.1.2]: https://github.com/nilsiker/godam/compare/v0.1.1...v0.1.2
[0.1.1]: https://github.com/nilsiker/godam/compare/v0.1.0...v0.1.1
[0.1.0]: https://github.com/nilsiker/godam/releases/tag/v0.1.0
