using Orion.Api;
using Orion.Api.Network;
using Orion.Protocol.Enums;
using Orion.Protocol.Packets;
using Orion.Protocol.Types;
using ApiBlockPos = Orion.Api.Math.BlockPos;
using ProtocolVec3f = Orion.Protocol.Types.Vec3f;

namespace OrionMining.Network;

/// <summary>LevelEvent / LevelSound helpers for crack and destroy FX.</summary>
internal static class CrackFx
{
    const float MinViewBlocks = 64f;

    public static void BroadcastCrack(IPlayer player, LevelEvent levelEvent, ApiBlockPos blockPosition, int data)
    {
        if (player.Dimension is null)
        {
            return;
        }

        ProtocolVec3f corner = BlockCorner(blockPosition);
        player.Dimension.Broadcast(
            new OpaqueOutboundPacket(new LevelEventPacket
            {
                Event = levelEvent,
                Position = corner,
                Data = data
            }),
            new PacketBroadcastOptions { MaxDistance = MinViewBlocks });
    }

    public static void BroadcastDestroyFx(IPlayer player, ApiBlockPos blockPosition, int networkId)
    {
        if (player.Dimension is null)
        {
            return;
        }

        ProtocolVec3f center = CenterOf(blockPosition);
        player.Dimension.Broadcast(new OpaqueOutboundPacket(new LevelEventPacket
        {
            Event = LevelEvent.ParticlesDestroyBlock,
            Position = center,
            Data = networkId
        }));

        player.Dimension.Broadcast(new OpaqueOutboundPacket(new LevelSoundEventPacket
        {
            Event = LevelSoundEvent.BreakBlock,
            Position = center,
            Data = networkId,
            ActorIdentifier = string.Empty,
            BabyMob = false,
            DisableRelativeVolume = false,
            UniqueActorId = 0,
            FireAtPosition = new Optional<ProtocolVec3f>()
        }));
    }

    static ProtocolVec3f BlockCorner(ApiBlockPos position) =>
        new() { X = position.X, Y = position.Y, Z = position.Z };

    static ProtocolVec3f CenterOf(ApiBlockPos position) =>
        new()
        {
            X = position.X + 0.5f,
            Y = position.Y + 0.5f,
            Z = position.Z + 0.5f
        };
}
