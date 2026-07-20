using Orion.Api;
using Orion.Api.Math;
using Orion.Gameplay;

namespace OrionMining;

/// <summary>
/// S7 Api-only façade. Deep crack/destroy previously depended on Orion.dll world/block types.
/// </summary>
public sealed class MiningGameplayServices : IMiningApi, IPlayerBlockBreakHandler
{
    public IPlayerBlockBreakHandler BlockBreak => this;

    public void OnStartDestroy(IPlayer player, BlockPos pos, int face, ulong tick) =>
        _ = (player, pos, face, tick);

    public void OnContinueDestroy(IPlayer player, BlockPos pos, int face, ulong tick) =>
        _ = (player, pos, face, tick);

    public void OnCrack(IPlayer player, BlockPos pos, int face, ulong tick) =>
        _ = (player, pos, face, tick);

    public void OnAbortDestroy(IPlayer player, BlockPos pos, int face) =>
        _ = (player, pos, face);

    public void OnPredictDestroy(IPlayer player, BlockPos pos, int face, ulong tick) =>
        _ = (player, pos, face, tick);

    public void OnCreativeDestroy(IPlayer player, BlockPos pos, int face) =>
        _ = (player, pos, face);
}
