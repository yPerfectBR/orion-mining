# Orion Mining

Opt-in vanilla block crack animation and destroy.

- **Manifest id:** `orion:mining`
- **Provides:** `orion:mining`
- **Soft depend:** `orion:inventory` (tool break-time + `NotifyBrokeBlock`)

## Build

```bash
dotnet build OrionMining.csproj -c Release
dotnet test tests/Orion.Plugins.Mining.Tests/Orion.Plugins.Mining.Tests.csproj -c Release
```

Requires Orion SDK **0.1.4+** (`Orion.Api`, `Orion.Gameplay.Api`, `Orion.Protocol`, `Orion.PluginContracts`).

## Behaviour

The host dispatches `AuthInput` block-destroy actions to `IPlayerBlockBreakHandler`. This plugin:

1. Validates reach (`6.5`) and crack duration (`BreakTime` from hardness + tool tags).
2. Emits `PlayerBreakBlockSignal` via `IServer.Emit` before mutating the world.
3. Honours `Cancel()` — reverts the client block with `UpdateBlock` and skips air/drops.
4. On success: destroy FX, `IBlock.NotifyBroken`, set air via `Blocks` + `SetPermutation`, broadcast `UpdateBlock`, then `IItemStack.NotifyBrokeBlock` when inventory is available.

Placement stays in **orion:building**.

## Cancel example

```csharp
context.Events.Subscribe<PlayerBreakBlockSignal>(signal =>
{
    if (signal.BlockPosition.Y >= 100)
        signal.Cancel();
}, EventPriority.High);
```

See `demo:break-guard`.

## CI

GitHub Actions smoke-boots the server with this plugin loaded after a Release build.
