# Fazlaka (فذلكة) — Windows Desktop

Official Windows desktop client for **Fazlaka**, an Arabic-first podcast platform. Browse seasons and episodes, stream audio, sign in with Google, and receive automatic updates.

## Tech Stack

- .NET 10 (`net10.0-windows10.0.26100.0`, min `10.0.19041.0`)
- WinUI 3 — Windows App SDK 1.8, unpackaged (`WindowsPackageType=None`)
- CommunityToolkit.Mvvm (MVVM source generators)
- Velopack (packaging + auto-updates)

## Build

```
dotnet restore
dotnet build
dotnet run --project src/Fazlaka.Windows
```

Requires Windows 10 (19041+) and the .NET 10 SDK.

## Configuration

| Setting | Value |
|---|---|
| API Base URL | `https://back-end-hq0is.faable.link/api/v1` |
| Google OAuth Client ID | `919871876990-hqb49huhl0gg2osdcg7jv7e39adf9fo1.apps.googleusercontent.com` |

## Brand Palette

| Token | Hex |
|---|---|
| Primary | `#8B5CF6` |
| Primary Dark | `#5B21B6` |
| Secondary / Glow | `#F59E0B` |
| Surface | `#151B2B` |
| Background | `#0B0F19` |
| Cyan | `#22D3EE` |

## Publishing

Releases are packaged with Velopack (`vpk pack`) and delivered through the built-in `UpdateService`.
