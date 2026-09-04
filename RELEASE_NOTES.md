#### 1.0.2 - September 4 2026

- Multi-targets `net8.0`, `net9.0` and `net10.0` (dropped `netstandard2.0`).
- Each target framework references the matching FSharp.Compiler.Service generation (43.8.403 / 43.9.303 / 43.12.400) so consumers on .NET 8, .NET 9 and .NET 10 get no NU1605/NU1608 FSharp.Core warnings.
- Requires a .NET 10 SDK (or newer) to build the library; `global.json` updated accordingly.

#### 1.0.1 - June 10 2022

- Removed the need for Thoth.Json.Net

#### 1.0.0 - June 9 2022

- Initial release
