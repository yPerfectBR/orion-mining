using System.Collections.Concurrent;
using Orion;
using Orion.Block.Traits.Types;
using Orion.Events;
using Orion.Gameplay;
using Orion.Item;
using Orion.Item.Traits.Types;
using Orion.Plugins;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using Orion.World;

namespace OrionMining;

/// <summary>
/// Basalt-aligned crack: StartBlockCracking once; Continue only restarts on position change.
/// PredictDestroy validates elapsed break time before mutating the world.
/// </summary>
internal static class BlockBreakHandler
{
    const float MaxBlockReachDistance = 6.5f;
    const ulong BreakToleranceTicks = 5;
    const UpdateBlockFlagsType NetworkUpdateFlags = UpdateBlockFlagsType.Network;

    static readonly ConcurrentDictionary<ulong, BreakState> BreakStates = new();

    readonly record struct BreakState(BlockPos Position, ulong StartTick, uint DurationTicks);

    public static void OnStartDestroy(global::Orion.Player.Player player, BlockPos pos, int face, ulong tick)
    {
        if (!IsBlockInReach(player, pos))
        {
            return;
        }

        RememberLastAction(player, pos, face);
        StartBreakBlock(player, pos, tick);
    }

    public static void OnContinueDestroy(global::Orion.Player.Player player, BlockPos pos, int face, ulong tick)
    {
        if (!IsBlockInReach(player, pos))
        {
            return;
        }

        RememberLastAction(player, pos, face);

        if (BreakStates.TryGetValue(player.RuntimeId, out BreakState existing)
            && SameBlock(existing.Position, pos))
        {
            RefreshCrackSpeed(player, pos, existing);
            return;
        }

        if (player.BreakingBlock.HasValue)
        {
            StopCrackBlock(player, player.BreakingBlock.Value);
        }

        StartBreakBlock(player, pos, tick);
    }

    public static void OnCrack(global::Orion.Player.Player player, BlockPos pos, int face, ulong tick)
    {
        if (!IsBlockInReach(player, pos))
        {
            return;
        }

        RememberLastAction(player, pos, face);

        if (!player.BreakingBlock.HasValue || !SameBlock(player.BreakingBlock.Value, pos))
        {
            StartBreakBlock(player, pos, tick);
            return;
        }

        if (BreakStates.TryGetValue(player.RuntimeId, out BreakState state))
        {
            RefreshCrackSpeed(player, pos, state);
        }
    }

    public static void OnAbortDestroy(global::Orion.Player.Player player, BlockPos pos, int face)
    {
        BlockPos target = player.BreakingBlock ?? pos;
        StopCrackBlock(player, target);
        BreakStates.TryRemove(player.RuntimeId, out _);
        player.BreakingBlock = null;
        _ = face;
    }

    public static void OnPredictDestroy(global::Orion.Player.Player player, BlockPos pos, int face, ulong tick)
    {
        BlockPos blockPosition = ResolveDestroyPosition(player, pos);
        if (IsZero(blockPosition) && !player.BreakingBlock.HasValue)
        {
            return;
        }

        if (!IsBlockInReach(player, blockPosition))
        {
            return;
        }

        RememberLastAction(player, blockPosition, face);

        bool valid = false;
        if (BreakStates.TryRemove(player.RuntimeId, out BreakState state)
            && SameBlock(state.Position, blockPosition))
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

        player.BreakingBlock = null;

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

    public static void OnCreativeDestroy(global::Orion.Player.Player player, BlockPos pos, int face)
    {
        BlockPos blockPosition = ResolveDestroyPosition(player, pos);
        if (IsZero(blockPosition) && !player.BreakingBlock.HasValue)
        {
            return;
        }

        if (!IsBlockInReach(player, blockPosition))
        {
            return;
        }

        RememberLastAction(player, blockPosition, face);
        BreakStates.TryRemove(player.RuntimeId, out _);
        StopCrackBlock(player, blockPosition);
        player.BreakingBlock = null;
        DestroyBlock(player, blockPosition, face);
    }

    static void StartBreakBlock(global::Orion.Player.Player player, BlockPos blockPosition, ulong tick)
    {
        if (player.BreakingBlock.HasValue && !SameBlock(player.BreakingBlock.Value, blockPosition))
        {
            StopCrackBlock(player, player.BreakingBlock.Value);
        }

        player.BreakingBlock = blockPosition;
        int breakTimeTicks = BreakTime.GetBreakTimeTicks(player, blockPosition);
        BreakStates[player.RuntimeId] = new BreakState(blockPosition, tick, (uint)breakTimeTicks);

        int crackSpeed = breakTimeTicks > 0
            ? Math.Min(65535, 65535 / breakTimeTicks)
            : 65535;

        // Bedrock expects the block's integer corner (not center) for crack overlay sync.
        BroadcastCrack(player, LevelEvent.StartBlockCracking, blockPosition, Math.Max(1, crackSpeed));
    }

    static void RefreshCrackSpeed(global::Orion.Player.Player player, BlockPos pos, BreakState state)
    {
        if (state.DurationTicks <= 1)
        {
            return;
        }

        int crackSpeed = Math.Max(1, Math.Min(65535, 65535 / (int)state.DurationTicks));
        BroadcastCrack(player, LevelEvent.UpdateBlockCracking, pos, crackSpeed);
    }

    static void StopCrackBlock(global::Orion.Player.Player player, BlockPos blockPosition)
    {
        BroadcastCrack(player, LevelEvent.StopBlockCracking, blockPosition, 0);
    }

    static void BroadcastCrack(
        global::Orion.Player.Player player,
        LevelEvent levelEvent,
        BlockPos blockPosition,
        int data)
    {
        if (player.Dimension is null)
        {
            return;
        }

        // Match movement/view FX radius so nearby peers always receive Start/Update/Stop crack.
        float viewBlocks = 64f;
        if (player.Session is not null)
        {
            int vd = player.GetTrait<Orion.Player.Traits.PlayerChunkRenderingTrait>()?.ViewDistance ?? 12;
            viewBlocks = Math.Max(64f, vd * 16f);
        }

        player.Dimension.Broadcast(
            new LevelEventPacket
            {
                Event = levelEvent,
                Position = BlockCorner(blockPosition),
                Data = data
            },
            new BroadcastOptions
            {
                Center = BlockCorner(blockPosition),
                Radius = viewBlocks
            });
    }

    static void DestroyBlock(global::Orion.Player.Player player, BlockPos blockPosition, int face)
    {
        if (player.Dimension is null)
        {
            return;
        }

        Orion.Block.BlockPermutation block =
            player.Dimension.GetGameplayPermutation(blockPosition.X, blockPosition.Y, blockPosition.Z);

        if (IsAirBlock(block) && player.Gamemode == Gamemode.Creative)
        {
            IPlayerInventoryAccess? creativeInventory = ResolveInventory(player);
            ItemStack? creativeHeldItem = creativeInventory?.GetHeldItem();
            int effectRuntime = creativeHeldItem is not null ? ItemBlockRuntimeIds.Resolve(creativeHeldItem.Type) : 0;
            if (effectRuntime == 0)
            {
                return;
            }

            BroadcastDestroyFx(player, CenterOf(blockPosition), effectRuntime);
            return;
        }

        if (IsAirBlock(block))
        {
            return;
        }

        Server? server = player.Dimension.World?.Server as Server;
        if (server is not null)
        {
            PlayerBreakBlockSignal signal = new(player, blockPosition, face);
            server.Emit(signal);
            if (!signal.Emit())
            {
                SendRevertBlock(player, blockPosition);

                IPlayerInventoryAccess? cancelInventory = ResolveInventory(player);
                if (cancelInventory is not null)
                {
                    ItemStack? rollbackItem = cancelInventory.GetHeldItem();
                    if (rollbackItem is not null)
                    {
                        cancelInventory.Container.SetItem(cancelInventory.SelectedSlot, rollbackItem.Clone());
                    }

                    cancelInventory.Container.UpdateSlot(cancelInventory.SelectedSlot);
                    cancelInventory.Container.Update();
                    cancelInventory.SyncToPlayer(player);
                }

                return;
            }
        }

        BroadcastDestroyFx(player, CenterOf(blockPosition), block.NetworkId);

        Orion.Block.BlockPermutation air = Orion.Block.BlockType
            .GetOrAir("minecraft:air")
            .GetPermutation();

        Orion.Block.Block breakingBlock =
            player.Dimension.GetBlock(blockPosition.X, blockPosition.Y, blockPosition.Z) ??
            new Orion.Block.Block(block);

        breakingBlock.OnBreak(new BlockBreakDetails(player, blockPosition));

        player.Dimension.SetGameplayPermutation(blockPosition.X, blockPosition.Y, blockPosition.Z, air);

        player.Dimension.Broadcast(new UpdateBlockPacket
        {
            Position = blockPosition,
            NetworkBlockId = air.NetworkId,
            Flags = NetworkUpdateFlags,
            Layer = UpdateBlockLayerType.Normal
        });

        IPlayerInventoryAccess? inventory = ResolveInventory(player);
        ItemStack? heldItem = inventory?.GetHeldItem();
        if (inventory is not null && heldItem is not null)
        {
            heldItem.OnBreakBlock(new ItemBreakBlockDetails(
                player,
                inventory.SelectedSlot,
                blockPosition,
                face));
        }
    }

    static void BroadcastDestroyFx(global::Orion.Player.Player player, Vec3f blockCenter, int networkId)
    {
        player.Dimension?.Broadcast(new LevelEventPacket
        {
            Event = LevelEvent.ParticlesDestroyBlock,
            Position = blockCenter,
            Data = networkId
        });

        player.Dimension?.Broadcast(new LevelSoundEventPacket
        {
            Event = LevelSoundEvent.BreakBlock,
            Position = blockCenter,
            Data = networkId,
            ActorIdentifier = string.Empty,
            BabyMob = false,
            DisableRelativeVolume = false,
            UniqueActorId = 0,
            FireAtPosition = new Optional<Vec3f> { HasValue = false, Value = default }
        });
    }

    static void SendRevertBlock(global::Orion.Player.Player player, BlockPos blockPosition)
    {
        if (player.Dimension is null)
        {
            return;
        }

        Orion.Block.BlockPermutation perm =
            player.Dimension.GetGameplayPermutation(blockPosition.X, blockPosition.Y, blockPosition.Z);

        player.Send(new UpdateBlockPacket
        {
            Position = blockPosition,
            NetworkBlockId = perm.NetworkId,
            Flags = NetworkUpdateFlags,
            Layer = UpdateBlockLayerType.Normal
        });
    }

    static BlockPos ResolveDestroyPosition(global::Orion.Player.Player player, BlockPos pos)
        => IsZero(pos) && player.BreakingBlock.HasValue ? player.BreakingBlock.Value : pos;

    static void RememberLastAction(global::Orion.Player.Player player, BlockPos pos, int face)
    {
        if (!IsZero(pos))
        {
            player.LastActionBlockPosition = pos;
            player.LastActionFace = face;
        }
    }

    static IPlayerInventoryAccess? ResolveInventory(global::Orion.Player.Player player)
    {
        if (PluginHost.Services.TryGet(out IPlayerInventoryService? service)
            && service is not null
            && service.TryGetAccess(player, out IPlayerInventoryAccess? access))
        {
            return access;
        }

        return null;
    }

    static bool IsAirBlock(Orion.Block.BlockPermutation block)
        => block.Type.Identifier is "minecraft:air" or "minecraft:cave_air" or "minecraft:void_air";

    static Vec3f BlockCorner(BlockPos position)
        => new() { X = position.X, Y = position.Y, Z = position.Z };

    static Vec3f CenterOf(BlockPos position)
        => new()
        {
            X = position.X + 0.5f,
            Y = position.Y + 0.5f,
            Z = position.Z + 0.5f
        };

    static bool SameBlock(BlockPos a, BlockPos b)
        => a.X == b.X && a.Y == b.Y && a.Z == b.Z;

    static bool IsZero(BlockPos position)
        => position.X == 0 && position.Y == 0 && position.Z == 0;

    static bool IsBlockInReach(global::Orion.Player.Player player, BlockPos blockPosition)
    {
        Vec3f blockCenter = CenterOf(blockPosition);
        float deltaX = blockCenter.X - player.Position.X;
        float deltaY = blockCenter.Y - player.Position.Y;
        float deltaZ = blockCenter.Z - player.Position.Z;
        float distanceSquared = deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ;
        return distanceSquared <= MaxBlockReachDistance * MaxBlockReachDistance;
    }
}
