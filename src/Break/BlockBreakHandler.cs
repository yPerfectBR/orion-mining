using Orion.Api;
using Orion.Api.Blocks;
using Orion.Api.Events;
using Orion.Api.Items;
using Orion.Api.Math;
using Orion.Api.Network;
using Orion.Gameplay;
using Orion.PluginContracts.Services;
using OrionMining.Network;
using LevelEvent = Orion.Protocol.Enums.LevelEvent;

namespace OrionMining.Break;

/// <summary>
/// Basalt-aligned crack: StartBlockCracking once; Continue only restarts on position change.
/// PredictDestroy validates elapsed break time before mutating the world.
/// </summary>
internal sealed class BlockBreakHandler
{
    const float MaxBlockReachDistance = 6.5f;
    const ulong BreakToleranceTicks = 5;

    readonly IServer _server;
    readonly IServiceRegistry _services;
    readonly BreakStateStore _states;

    public BlockBreakHandler(IServer server, IServiceRegistry services, BreakStateStore states)
    {
        _server = server;
        _services = services;
        _states = states;
    }

    public void OnStartDestroy(IPlayer player, BlockPos pos, int face, ulong tick)
    {
        if (!IsBlockInReach(player, pos))
        {
            return;
        }

        RememberLastAction(player, pos, face);
        StartBreakBlock(player, pos, tick);
    }

    public void OnContinueDestroy(IPlayer player, BlockPos pos, int face, ulong tick)
    {
        if (!IsBlockInReach(player, pos))
        {
            return;
        }

        RememberLastAction(player, pos, face);
        BreakStateStore.PlayerBreakSession session = _states.GetOrCreate(player.RuntimeId);

        if (session.State is { } existing && SameBlock(existing.Position, pos))
        {
            RefreshCrackSpeed(player, pos, existing);
            return;
        }

        if (session.BreakingBlock is { } previous)
        {
            StopCrackBlock(player, previous);
        }

        StartBreakBlock(player, pos, tick);
    }

    public void OnCrack(IPlayer player, BlockPos pos, int face, ulong tick)
    {
        if (!IsBlockInReach(player, pos))
        {
            return;
        }

        RememberLastAction(player, pos, face);
        BreakStateStore.PlayerBreakSession session = _states.GetOrCreate(player.RuntimeId);

        if (session.BreakingBlock is not { } breaking || !SameBlock(breaking, pos))
        {
            StartBreakBlock(player, pos, tick);
            return;
        }

        if (session.State is { } state)
        {
            RefreshCrackSpeed(player, pos, state);
        }
    }

    public void OnAbortDestroy(IPlayer player, BlockPos pos, int face)
    {
        BreakStateStore.PlayerBreakSession session = _states.GetOrCreate(player.RuntimeId);
        BlockPos target = session.BreakingBlock ?? pos;
        StopCrackBlock(player, target);
        session.State = null;
        session.BreakingBlock = null;
        _ = face;
    }

    public void OnPredictDestroy(IPlayer player, BlockPos pos, int face, ulong tick)
    {
        BreakStateStore.PlayerBreakSession session = _states.GetOrCreate(player.RuntimeId);
        BlockPos blockPosition = ResolveDestroyPosition(session, pos);
        if (IsZero(blockPosition) && session.BreakingBlock is null)
        {
            return;
        }

        if (!IsBlockInReach(player, blockPosition))
        {
            return;
        }

        RememberLastAction(player, blockPosition, face);

        bool valid = false;
        if (session.State is { } state && SameBlock(state.Position, blockPosition))
        {
            ulong elapsed = tick >= state.StartTick ? tick - state.StartTick : 0;
            if (state.DurationTicks <= 1)
            {
                valid = true;
            }
            else if (elapsed > 0)
            {
                valid = elapsed + BreakToleranceTicks >= state.DurationTicks;
            }
        }

        session.State = null;
        session.BreakingBlock = null;

        if (!valid)
        {
            if (player.Gamemode == Gamemode.Creative)
            {
                StopCrackBlock(player, blockPosition);
                DestroyBlock(player, blockPosition, face);
                return;
            }

            StopCrackBlock(player, blockPosition);
            SendRevertBlock(player, blockPosition);
            return;
        }

        StopCrackBlock(player, blockPosition);
        DestroyBlock(player, blockPosition, face);
    }

    public void OnCreativeDestroy(IPlayer player, BlockPos pos, int face)
    {
        BreakStateStore.PlayerBreakSession session = _states.GetOrCreate(player.RuntimeId);
        BlockPos blockPosition = ResolveDestroyPosition(session, pos);
        if (IsZero(blockPosition) && session.BreakingBlock is null)
        {
            return;
        }

        if (!IsBlockInReach(player, blockPosition))
        {
            return;
        }

        RememberLastAction(player, blockPosition, face);
        session.State = null;
        StopCrackBlock(player, blockPosition);
        session.BreakingBlock = null;
        DestroyBlock(player, blockPosition, face);
    }

    void StartBreakBlock(IPlayer player, BlockPos blockPosition, ulong tick)
    {
        BreakStateStore.PlayerBreakSession session = _states.GetOrCreate(player.RuntimeId);
        if (session.BreakingBlock is { } previous && !SameBlock(previous, blockPosition))
        {
            StopCrackBlock(player, previous);
        }

        session.BreakingBlock = blockPosition;
        int breakTimeTicks = BreakTime.GetBreakTimeTicks(player, blockPosition, _services);
        session.State = new BreakStateStore.BreakState(blockPosition, tick, (uint)breakTimeTicks);

        int crackSpeed = breakTimeTicks > 0
            ? Math.Min(65535, 65535 / breakTimeTicks)
            : 65535;

        CrackFx.BroadcastCrack(player, LevelEvent.StartBlockCracking, blockPosition, Math.Max(1, crackSpeed));
    }

    static void RefreshCrackSpeed(IPlayer player, BlockPos pos, BreakStateStore.BreakState state)
    {
        if (state.DurationTicks <= 1)
        {
            return;
        }

        int crackSpeed = Math.Max(1, Math.Min(65535, 65535 / (int)state.DurationTicks));
        CrackFx.BroadcastCrack(player, LevelEvent.UpdateBlockCracking, pos, crackSpeed);
    }

    static void StopCrackBlock(IPlayer player, BlockPos blockPosition) =>
        CrackFx.BroadcastCrack(player, LevelEvent.StopBlockCracking, blockPosition, 0);

    void DestroyBlock(IPlayer player, BlockPos blockPosition, int face)
    {
        if (player.Dimension is null)
        {
            return;
        }

        IBlock? block = player.Dimension.GetBlock(blockPosition.X, blockPosition.Y, blockPosition.Z);
        IBlockPermutation permutation = block?.Permutation
            ?? player.Dimension.GetPermutation(blockPosition.X, blockPosition.Y, blockPosition.Z);

        if (IsAirBlock(permutation) && player.Gamemode == Gamemode.Creative)
        {
            IItemStack? creativeHeld = ResolveHeldItem(player);
            int effectRuntime = creativeHeld?.Type.NetworkId ?? 0;
            if (effectRuntime == 0)
            {
                return;
            }

            CrackFx.BroadcastDestroyFx(player, blockPosition, effectRuntime);
            return;
        }

        if (IsAirBlock(permutation))
        {
            return;
        }

        PlayerBreakBlockSignal signal = new(player, blockPosition, face);
        _server.Emit(signal);
        if (!signal.Emit())
        {
            SendRevertBlock(player, blockPosition);
            RollbackHeldItem(player);
            return;
        }

        CrackFx.BroadcastDestroyFx(player, blockPosition, permutation.NetworkId);

        IBlockPermutation? air = Blocks.TryGetDefaultPermutation("minecraft:air");
        if (air is null)
        {
            return;
        }

        IBlock breakingBlock = block ?? Blocks.TryCreate(permutation.Type.Identifier)
            ?? Blocks.Create("minecraft:air");
        breakingBlock.NotifyBroken(player, blockPosition);

        player.Dimension.SetPermutation(blockPosition.X, blockPosition.Y, blockPosition.Z, air);
        player.Dimension.Broadcast(BlockNetwork.CreateUpdateBlock(blockPosition, air.NetworkId));

        IPlayerInventoryAccess? inventory = ResolveInventory(player);
        IItemStack? heldItem = inventory?.GetHeldItem();
        if (inventory is not null && heldItem is not null)
        {
            heldItem.NotifyBrokeBlock(player, blockPosition, face, inventory.SelectedSlot);
        }
    }

    static void SendRevertBlock(IPlayer player, BlockPos blockPosition)
    {
        if (player.Dimension is null)
        {
            return;
        }

        IBlockPermutation perm = player.Dimension.GetPermutation(blockPosition.X, blockPosition.Y, blockPosition.Z);
        player.Send(BlockNetwork.CreateUpdateBlock(blockPosition, perm.NetworkId));
    }

    void RememberLastAction(IPlayer player, BlockPos pos, int face)
    {
        if (IsZero(pos))
        {
            return;
        }

        BreakStateStore.PlayerBreakSession session = _states.GetOrCreate(player.RuntimeId);
        session.LastActionBlockPosition = pos;
        session.LastActionFace = face;
    }

    IPlayerInventoryAccess? ResolveInventory(IPlayer player)
    {
        if (_services.TryGet(out IPlayerInventoryService? service)
            && service is not null
            && service.TryGetAccess(player, out IPlayerInventoryAccess? access))
        {
            return access;
        }

        return null;
    }

    IItemStack? ResolveHeldItem(IPlayer player) => ResolveInventory(player)?.GetHeldItem();

    void RollbackHeldItem(IPlayer player)
    {
        IPlayerInventoryAccess? inventory = ResolveInventory(player);
        if (inventory is null)
        {
            return;
        }

        IItemStack? rollbackItem = inventory.GetHeldItem();
        if (rollbackItem is not null)
        {
            inventory.Container.SetItem(inventory.SelectedSlot, rollbackItem.Clone());
        }

        inventory.Container.UpdateSlot(inventory.SelectedSlot);
        inventory.Container.Update();
        inventory.SyncToPlayer(player);
    }

    static BlockPos ResolveDestroyPosition(BreakStateStore.PlayerBreakSession session, BlockPos pos)
        => IsZero(pos) && session.BreakingBlock is { } breaking ? breaking : pos;

    static bool IsAirBlock(IBlockPermutation block)
    {
        string id = block.Type.Identifier;
        return id is "minecraft:air" or "minecraft:cave_air" or "minecraft:void_air" || block.Type.Air;
    }

    static bool SameBlock(BlockPos a, BlockPos b)
        => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    static bool IsZero(BlockPos position)
        => position.X == 0 && position.Y == 0 && position.Z == 0;

    static bool IsBlockInReach(IPlayer player, BlockPos blockPosition)
    {
        float centerX = blockPosition.X + 0.5f;
        float centerY = blockPosition.Y + 0.5f;
        float centerZ = blockPosition.Z + 0.5f;
        float deltaX = centerX - player.Position.X;
        float deltaY = centerY - player.Position.Y;
        float deltaZ = centerZ - player.Position.Z;
        float distanceSquared = deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ;
        return distanceSquared <= MaxBlockReachDistance * MaxBlockReachDistance;
    }
}
