using Orion.Api;
using Orion.Api.Math;
using Orion.Gameplay;
using Orion.PluginContracts.Services;
using OrionMining.Break;

namespace OrionMining;

/// <summary>Api-only façade: crack/destroy validation, signal Emit, and world mutation.</summary>
public sealed class MiningGameplayServices : IMiningApi, IPlayerBlockBreakHandler
{
    readonly BlockBreakHandler _handler;

    public MiningGameplayServices(IServer server, IServiceRegistry services)
    {
        _handler = new BlockBreakHandler(server, services, new BreakStateStore());
    }

    public IPlayerBlockBreakHandler BlockBreak => this;

    public void OnStartDestroy(IPlayer player, BlockPos pos, int face, ulong tick) =>
        _handler.OnStartDestroy(player, pos, face, tick);

    public void OnContinueDestroy(IPlayer player, BlockPos pos, int face, ulong tick) =>
        _handler.OnContinueDestroy(player, pos, face, tick);

    public void OnCrack(IPlayer player, BlockPos pos, int face, ulong tick) =>
        _handler.OnCrack(player, pos, face, tick);

    public void OnAbortDestroy(IPlayer player, BlockPos pos, int face) =>
        _handler.OnAbortDestroy(player, pos, face);

    public void OnPredictDestroy(IPlayer player, BlockPos pos, int face, ulong tick) =>
        _handler.OnPredictDestroy(player, pos, face, tick);

    public void OnCreativeDestroy(IPlayer player, BlockPos pos, int face) =>
        _handler.OnCreativeDestroy(player, pos, face);
}
