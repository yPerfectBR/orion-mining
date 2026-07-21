# Orion Mining

Opt-in vanilla block crack animation and destroy.

- **Manifest id:** `orion:mining`
- **Provides:** `orion:mining`
- **Soft depend:** `orion:inventory` (tool break-time + `OnBreakBlock`)

## Build

```bash
dotnet build OrionMining.csproj -c Release
```

## API

Registered services: `IMiningApi`, `IPlayerBlockBreakHandler`.

The core dispatches `AuthInput` block-destroy actions here. Placement stays in **orion:building**.

## CI

GitHub Actions smoke-boots the server with this plugin loaded after a Release build.
