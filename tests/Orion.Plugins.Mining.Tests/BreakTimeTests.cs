using Orion.Api.Events;
using Orion.Api.Math;
using OrionMining.Break;

namespace Orion.Plugins.Mining.Tests;

public sealed class BreakTimeTests
{
    [Fact]
    public void BareHand_OnSolidWithZeroHardness_UsesDefaultDigDuration()
    {
        int ticks = BreakTime.ComputeTicks(
            hardness: 0f,
            air: false,
            solid: true,
            blockTags: [],
            itemTags: null);

        // 0.6 hardness * 1.5 incompatible? requiredTier 0 → compatible → 1.5
        // seconds = 0.6 * 1.5 / 1 = 0.9 → ceil(0.9*20) = 18
        Assert.Equal(18, ticks);
    }

    [Fact]
    public void AirBlock_ReturnsOneTick()
    {
        int ticks = BreakTime.ComputeTicks(0f, air: true, solid: false, blockTags: [], itemTags: null);
        Assert.Equal(1, ticks);
    }

    [Fact]
    public void Unbreakable_ReturnsMaxTicks()
    {
        int ticks = BreakTime.ComputeTicks(-1f, air: false, solid: true, blockTags: [], itemTags: null);
        Assert.Equal(6000, ticks);
    }

    [Fact]
    public void MatchingPickaxe_IsFasterThanBareHand_OnStoneTaggedBlock()
    {
        string[] blockTags = ["minecraft:is_pickaxe_item_destructible"];
        int bare = BreakTime.ComputeTicks(1.5f, false, true, blockTags, null);
        int withPick = BreakTime.ComputeTicks(
            1.5f,
            false,
            true,
            blockTags,
            ["minecraft:is_pickaxe", "minecraft:iron_tier"]);

        Assert.True(withPick < bare);
    }
}

public sealed class PlayerBreakBlockSignalCancelTests
{
    [Fact]
    public void Cancelled_Signal_Emit_ReturnsFalse()
    {
        // Mirror DestroyBlock gate: Emit then check signal.Emit()
        var signal = new PlayerBreakBlockSignal(new StubPlayer(), new BlockPos(0, 100, 0), blockFace: 1);
        signal.Cancel();
        Assert.False(signal.Emit());
    }

    [Fact]
    public void Uncancelled_Signal_Emit_ReturnsTrue()
    {
        var signal = new PlayerBreakBlockSignal(new StubPlayer(), new BlockPos(1, 2, 3), blockFace: 0);
        Assert.True(signal.Emit());
    }

    sealed class StubPlayer : Orion.Api.IPlayer
    {
        public long UniqueId => 0;
        public ulong RuntimeId => 0;
        public string TypeIdentifier => "minecraft:player";
        public Orion.Api.IDimension? Dimension => null;
        public Orion.Api.Math.Vec3f Position => default;
        public bool IsPlayer() => true;
        public T? GetTrait<T>() where T : class => null;
        public void NotifyContainerUpdate(Orion.Api.Containers.IContainer container) { }
        public string Username => "stub";
        public string Xuid => "";
        public Guid Uuid => Guid.Empty;
        public bool IsOnline => true;
        public bool Spawned => true;
        public bool IsOperator => false;
        public Orion.Api.Gamemode Gamemode => Orion.Api.Gamemode.Survival;
        public void SetGamemode(Orion.Api.Gamemode gamemode) { }
        public void SendMessage(string message) { }
        public void Disconnect(string reason = "") { }
        public void Teleport(Orion.Api.Math.Vec3f position, Orion.Api.IDimension? dimension = null, bool forceDimensionChange = false) { }
        public void Send(params Orion.Api.Network.IOutboundPacket[] packets) { }
        public void SetHud(Orion.Api.HudVisibility visibility, params Orion.Api.HudElement[] elements) { }
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
