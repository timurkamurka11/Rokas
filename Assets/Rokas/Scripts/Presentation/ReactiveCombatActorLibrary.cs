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
        public AnimationClip preparation;
        public AnimationClip heavyPreparation;
        public AnimationClip enterBattle;
        public AnimationClip entranceWalk;
        public AnimationClip guard;
        public AnimationClip dodge;
        public AnimationClip hit;
        public AnimationClip stagger;
        public AnimationClip death;
        public AnimationClip walk;
        public AnimationClip approach;
        public AnimationClip returnHome;
        // Original FBX rig units per complete cycle, before any stage/slot scale.
        public float approachStrideDistance = 1.8f;
        public float returnStrideDistance = 1.8f;
        public float entranceStrideDistance = 1.8f;
        public float guardContactSeconds = .34f;
        public float dodgeContactSeconds = .24f;
        public float standingHeight = 2f;
        public float forwardYaw;
        [Range(0f, 1f)] public float attackContactNormalized = .5f;
        [Range(0f, 1f)] public float heavyContactNormalized = .5f;
        public GameObject weaponPrefab;
        public string weaponBonePath;
        public Vector3 weaponSocketPosition;
        public Vector3 weaponSocketEuler;
        public Vector3 weaponSocketScale = Vector3.one;
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
