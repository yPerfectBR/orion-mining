using Orion.Api;
using Orion.Api.Blocks;
using Orion.Api.Events;
using Orion.Api.Math;
using Orion.Api.Network;
using Orion.Gameplay;
using Orion.PluginContracts.Services;
using OrionMining.Break;

namespace Orion.Plugins.Mining.Tests;

public sealed class BlockBreakHandlerCancelTests
{
    [Fact]
    public void Destroy_WhenSignalCancelled_DoesNotSetAir()
    {
        var dimension = new FakeDimension();
        var player = new FakePlayer(dimension) { Position = new Vec3f(1.5f, 1.5f, 1.5f) };
        var server = new CancellingServer();
        var services = new EmptyServices();
        var handler = new BlockBreakHandler(server, services, new BreakStateStore());

        // Creative destroy bypasses break-time validation.
        player.Gamemode = Gamemode.Creative;
        handler.OnCreativeDestroy(player, new BlockPos(1, 1, 1), face: 0);

        Assert.True(server.Emitted);
        Assert.False(dimension.PermutationChanged);
        Assert.True(player.SentPackets.Count > 0);
    }

    sealed class CancellingServer : IServer
    {
        public bool Emitted { get; private set; }
        public IReadOnlyCollection<IPlayer> OnlinePlayers { get; } = [];
        public IWorld? DefaultWorld => null;
        public IWorld? GetWorld(string name) => null;
        public IReadOnlyCollection<IWorld> Worlds { get; } = [];

        public void Emit(ISignal signal)
        {
            Emitted = true;
            if (signal is PlayerBreakBlockSignal breakSignal)
            {
                breakSignal.Cancel();
            }
        }
    }

    sealed class EmptyServices : IServiceRegistry
    {
        public void Register<TService>(TService instance, Orion.PluginContracts.IOrionPlugin owner, ServicePriority priority = ServicePriority.Normal)
            where TService : class
        {
        }

        public void UnregisterAll(Orion.PluginContracts.IOrionPlugin owner) { }

        public bool TryGet<TService>(out TService? service) where TService : class
        {
            service = null;
            return false;
        }

        public TService GetRequired<TService>() where TService : class =>
            throw new InvalidOperationException();
    }

    sealed class FakeDimension : IDimension
    {
        public bool PermutationChanged { get; private set; }
        public bool RevertSent { get; private set; }
        public string Name => "overworld";
        public IWorld World { get; } = new FakeWorld();

        readonly FakeBlock _stone = new();

        public IBlock? GetBlock(int x, int y, int z, int layer = 0) => _stone;

        public void SetBlock(int x, int y, int z, IBlock block, int layer = 0, bool dirty = true) =>
            PermutationChanged = true;

        public IBlockPermutation GetPermutation(int x, int y, int z, int layer = 0) =>
            _stone.Permutation;

        public void SetPermutation(int x, int y, int z, IBlockPermutation permutation, int layer = 0, bool dirty = true) =>
            PermutationChanged = true;

        public IEntity SpawnEntity(string typeIdentifier, Vec3f position, EntitySpawnOptions? options = null) =>
            throw new NotSupportedException();

        public IReadOnlyCollection<IEntity> GetEntities() => [];

        public void Broadcast(IOutboundPacket packet, PacketBroadcastOptions? options = null) { }
    }

    sealed class FakeWorld : IWorld
    {
        public string Name => "world";
        public IDimension? GetDimension(string name) => null;
        public IReadOnlyCollection<IDimension> Dimensions { get; } = [];
    }

    sealed class FakeBlock : IBlock
    {
        public IBlockType Type { get; } = new FakeBlockType();
        public IBlockPermutation Permutation { get; }

        public FakeBlock() => Permutation = new FakePermutation(Type);

        public void NotifyBroken(IPlayer breaker, BlockPos blockPosition) { }
    }

    sealed class FakeBlockType : IBlockType
    {
        public string Identifier => "minecraft:stone";
        public float Hardness => 1.5f;
        public bool Solid => true;
        public bool Air => false;
        public IReadOnlyList<string> Tags { get; } = [];
    }

    sealed class FakePermutation(IBlockType type) : IBlockPermutation
    {
        public IBlockType Type { get; } = type;
        public int NetworkId => 1;
    }

    sealed class FakePlayer(IDimension dimension) : IPlayer
    {
        public List<IOutboundPacket> SentPackets { get; } = [];
        public long UniqueId => 1;
        public ulong RuntimeId => 1;
        public string TypeIdentifier => "minecraft:player";
        public IDimension? Dimension { get; } = dimension;
        public Vec3f Position { get; set; }
        public bool IsPlayer() => true;
        public T? GetTrait<T>() where T : class => null;
        public void NotifyContainerUpdate(Orion.Api.Containers.IContainer container) { }
        public string Username => "tester";
        public string Xuid => "";
        public Guid Uuid => Guid.Empty;
        public bool IsOnline => true;
        public bool Spawned => true;
        public bool IsOperator => false;
        public Gamemode Gamemode { get; set; } = Gamemode.Survival;
        public void SetGamemode(Gamemode gamemode) => Gamemode = gamemode;
        public void SendMessage(string message) { }
        public void Disconnect(string reason = "") { }
        public void Teleport(Vec3f position, IDimension? dim = null, bool forceDimensionChange = false) { }
        public void Send(params IOutboundPacket[] packets) => SentPackets.AddRange(packets);
        public void SetHud(HudVisibility visibility, params HudElement[] elements) { }
        public bool DropItem(Orion.Api.Items.IItemStack item) => false;
        public void SyncInventoryToClient() { }
        public IReadOnlyDictionary<int, Orion.Api.Containers.IContainer> OpenedContainers { get; } =
            new Dictionary<int, Orion.Api.Containers.IContainer>();
        public void RegisterOpenContainer(int windowId, Orion.Api.Containers.IContainer container) { }
        public bool TryGetOpenContainer(int windowId, out Orion.Api.Containers.IContainer? container)
        {
            container = null;
            return false;
        }
        public void UnregisterOpenContainer(int windowId) { }
        public void FlushPendingClientSync(bool force = false) { }
        public bool HasPermission(string permission) => false;
    }
}
