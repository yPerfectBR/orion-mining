using Orion.Gameplay;
using Orion.Protocol.Types;

namespace OrionMining;

/// <summary>
/// Host facade for block crack / destroy.
/// </summary>
public sealed class MiningGameplayServices : IMiningApi, IPlayerBlockBreakHandler
{
    public IPlayerBlockBreakHandler BlockBreak => this;

    public void OnStartDestroy(global::Orion.Player.Player player, BlockPos pos, int face, ulong tick)
        => BlockBreakHandler.OnStartDestroy(player, pos, face, tick);

    public void OnContinueDestroy(global::Orion.Player.Player player, BlockPos pos, int face, ulong tick)
        => BlockBreakHandler.OnContinueDestroy(player, pos, face, tick);

    public void OnCrack(global::Orion.Player.Player player, BlockPos pos, int face, ulong tick)
        => BlockBreakHandler.OnCrack(player, pos, face, tick);

    public void OnAbortDestroy(global::Orion.Player.Player player, BlockPos pos, int face)
        => BlockBreakHandler.OnAbortDestroy(player, pos, face);

    public void OnPredictDestroy(global::Orion.Player.Player player, BlockPos pos, int face, ulong tick)
        => BlockBreakHandler.OnPredictDestroy(player, pos, face, tick);

    public void OnCreativeDestroy(global::Orion.Player.Player player, BlockPos pos, int face)
        => BlockBreakHandler.OnCreativeDestroy(player, pos, face);
}
