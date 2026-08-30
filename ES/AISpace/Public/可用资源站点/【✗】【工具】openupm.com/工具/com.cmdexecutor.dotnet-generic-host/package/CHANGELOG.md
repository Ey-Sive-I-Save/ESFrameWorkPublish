# Microsoft.Extensions.Hosting for Unity

## [3.0.0] - 2023-07-07
### Updated
- rework Unity objects registration
- allow to use HostBuilderContext in registrations
- update Microsoft libs to version 7
- add sample Unity project

## [2.1.0] - 2023-05-03
### Added
- Scoped MonoBehaviours
- `MonoBehaviour` prefabs registration by prefab name from Resources
- `ScriptableObjectConfiguration` as configuration source
- hostBuilding event when the host is being built
- `StopManuallyAsync` method to `HostManager`
- Replace `Host` with `UnityHost` to remove console lifetime that was preventing IL2CPP building on Windows
### Updated
- make HostManager events public

## [2.0.0] - 2022-07-21
### Updated
- .NET libraries updated to v6.0.0
### Added
- AddDetachedMonoBehaviourSingleton to add a MonoBehaviour service as a separated GameObject
### Fixed
- Default GlobalSettings initialization issue

## [1.2.4] - 2021-09-17
### Fixed version issues

## [1.2.1] - 2021-09-17
### Added
- HostManager events changed from private to protected
### Fixed
- Trace output formatting for unity logger

## [1.2.0] - 2021-07-23
### Fixed
- Removed reload on change for FileConfigurationSources (caused UnityEditor to enter play mode with long delay)

### Added
- ScriptableObject registration
- MonoBehaviour HostedService registration
- Possibility to pass args to the host
- Host log level parameter in the host Inspector
- Host graceful shutduwn timeout parameter in the host Inspector

## [1.1.0] - 2021-05-27
### Fixed
- Dependency resolving for MonoBehaviours

## [1.0.4] - 2021-05-27
### Fixed
- Dependency resolving for MonoBehaviours

## [1.0.3] - 2021-05-13
### Fixed
- Minor fixes

## [1.0.2] - 2021-04-22
### Added
- MonoBeaviour component registration from existing GameObject

## [1.0.1] - 2021-04-22
### Added
- Host lifetime Unity Events for HostManager

## [1.0.0] - 2021-04-19
### Initial release