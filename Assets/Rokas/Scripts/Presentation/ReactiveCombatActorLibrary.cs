using System;
using UnityEngine;

namespace Rokas.Presentation
{
    public enum CombatActorKind { Keiko, Mina, Yokai }

    [Serializable]
    public sealed class ReactiveCombatActorClips
    {
        public GameObject model;
        public Material material;
        public AnimationClip idle;
        public AnimationClip attack;
        public AnimationClip heavy;
        public AnimationClip hit;
        public AnimationClip stagger;
        public AnimationClip death;
        public AnimationClip walk;
        public AnimationClip approach;
        public AnimationClip returnHome;
        public float standingHeight = 2f;
        public float forwardYaw;
    }

    // Only referenced model and clip subassets enter the player build.
    public sealed class ReactiveCombatActorLibrary : ScriptableObject
    {
        public ReactiveCombatActorClips keiko = new ReactiveCombatActorClips();
        public ReactiveCombatActorClips mina = new ReactiveCombatActorClips();
        public ReactiveCombatActorClips yokai = new ReactiveCombatActorClips();

        public ReactiveCombatActorClips Get(CombatActorKind kind)
        {
            switch (kind)
            {
                case CombatActorKind.Keiko: return keiko;
                case CombatActorKind.Mina: return mina;
                case CombatActorKind.Yokai: return yokai;
                default: throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }
    }
}
