using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using System.Numerics;
using CSCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;
using CSGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace AutoFateGrind.Core.Game.Fates;

internal static unsafe class FateMobScanner
{
    public static bool TryFindNearestMob(uint fateId, Vector3 from, out Vector3 position, out float distance)
    {
        var found = TryFindNearestNpc(fateId, from, out var mob, out distance);
        position = mob?.Position ?? default;
        return found;
    }

    // Friendly combatants (the Yellowjacket guards in Lower La Noscea) are FATE-tagged too; only an enemy counts.
    public static bool IsHostile(IBattleNpc npc) => ((CSCharacter*)npc.Address)->IsHostile;

    public static bool IsFateMob(IBattleNpc npc, uint fateId)
        => ((CSGameObject*)npc.Address)->FateId == fateId && IsHostile(npc);

    public static bool TryFindNearestNpc(uint fateId, Vector3 from, out IBattleNpc? mob, out float distance)
    {
        mob = null;
        distance = float.MaxValue;

        var objects = Svc.Objects;
        for (var index = 0; index < objects.Length; index++)
        {
            if (objects[index] is not IBattleNpc npc) continue;
            if (!npc.IsTargetable) continue;
            if (npc.CurrentHp == 0) continue;

            var native = (CSGameObject*)npc.Address;
            if (native->FateId != fateId) continue;
            if (native->BattleNpcSubKind != BattleNpcSubKind.Combatant) continue;
            if (!IsHostile(npc)) continue;

            var candidate = Vector3.Distance(from, npc.Position);
            if (candidate >= distance) continue;

            distance = candidate;
            mob = npc;
        }

        return distance < float.MaxValue;
    }
}
