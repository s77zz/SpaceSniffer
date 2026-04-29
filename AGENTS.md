# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Build & Run

```bash
# Build the project
dotnet build

# Build in Release mode
dotnet build -c Release

# Run the application
dotnet run --project SpaceSniffer/SpaceSniffer.csproj

# Run with specific configuration
dotnet run -c Release --project SpaceSniffer/SpaceSniffer.csproj
```

## Architecture

WPF desktop application targeting **.NET 9.0-windows** with `UseWPF` enabled. The project is currently a scaffold (fresh from template) intended to become a disk space visualization/analysis tool.

### Project structure

- **`SpaceSniffer.sln`** — Solution file (VS 2022+, format v12)
- **`SpaceSniffr/`** — Main WPF project
  - **`App.xaml` / `App.xaml.cs`** — Application entry point. `StartupUri` points to `MainWindow.xaml`.
  - **`MainWindow.xaml` / `MainWindow.xaml.cs`** — Main application window (currently empty).
  - **`AssemblyInfo.cs`** — Theme resource dictionary location configuration.
  - **`SpaceSniffer.csproj`** — Project file: WinExe output, nullable enabled, implicit usings on.

### Key conventions

- XAML-first: UI defined in `.xaml` files, code-behind in `.xaml.cs` partial classes.
- WPF `Grid`-based layout for window composition.
- Standard WPF data binding patterns (the `MainWindow.xaml` includes blend/schema namespaces ready for binding).
