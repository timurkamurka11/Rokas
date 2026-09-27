using System;

namespace Rokas.Core.ReactiveTurns
{
    public static class EnemyPolicy
    {
        // The first slice uses authored teaching order. A saved turn count preserves intent across reload.
        public static string ChooseAttack(ActorDefinition actor, int naturalTurns, long seed)
        {
            if (actor == null) throw new ArgumentNullException("actor");
            if (actor.IsHunter || actor.AttackSequenceIds.Count == 0) throw new InvalidOperationException("Enemy has no attack sequence.");
            if (naturalTurns < 0) throw new ArgumentOutOfRangeException("naturalTurns");
            return actor.AttackSequenceIds[naturalTurns % actor.AttackSequenceIds.Count];
        }
    }
}
