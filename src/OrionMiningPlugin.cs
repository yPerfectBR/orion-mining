using Orion.Gameplay;
using Orion.PluginContracts;

namespace OrionMining;

public sealed class OrionMiningPlugin : IOrionPlugin
{
    public string Id => "orion:mining";

    public Version Version { get; } = new(1, 0, 0);

    public void Load(IPluginLoadContext context) => _ = context;

    public void OnEnable(IPluginContext context)
    {
        MiningGameplayServices services = new();
        context.Services.Register<IMiningApi>(services, this);
        context.Services.Register<IPlayerBlockBreakHandler>(services, this);
    }

    public void OnWorldInitialize(IWorldInitContext context) => _ = context;

    public void OnDisable(IPluginContext context) => _ = context;
}
