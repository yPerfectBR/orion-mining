using System.Collections.Concurrent;
using Orion.Api.Math;

namespace OrionMining.Break;

/// <summary>Per-player crack/destroy state keyed by <see cref="Orion.Api.IEntity.RuntimeId"/>.</summary>
internal sealed class BreakStateStore
{
    readonly ConcurrentDictionary<ulong, PlayerBreakSession> _sessions = new();

    internal readonly record struct BreakState(BlockPos Position, ulong StartTick, uint DurationTicks);

    internal sealed class PlayerBreakSession
    {
        public BlockPos? BreakingBlock;
        public BreakState? State;
        public BlockPos? LastActionBlockPosition;
        public int LastActionFace;
    }

    public PlayerBreakSession GetOrCreate(ulong runtimeId) =>
        _sessions.GetOrAdd(runtimeId, static _ => new PlayerBreakSession());

    public bool TryGet(ulong runtimeId, out PlayerBreakSession session) =>
        _sessions.TryGetValue(runtimeId, out session!);

    public void ClearState(ulong runtimeId)
    {
        if (_sessions.TryGetValue(runtimeId, out PlayerBreakSession? session))
        {
            session.State = null;
            session.BreakingBlock = null;
        }
    }

    public void Remove(ulong runtimeId) => _sessions.TryRemove(runtimeId, out _);
}
