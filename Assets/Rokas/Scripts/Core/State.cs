using System;
using System.Collections.Generic;
using Rokas.Core.ReactiveTurns;

namespace Rokas.Core
{
    public enum RunPhase
    {
        Home,
        Accepted,
        Portal,
        Combat,
        Sealed,
        Payment,
        Failed
    }

    public enum CombatMode
    {
        Legacy,
        ReactiveTurns
    }

    [Serializable]
    public sealed class SettingsData
    {
        public float masterVolume = .7f;
        public float musicVolume = .45f;
        public float sfxVolume = .7f;
        public bool screenShake = true;
        public float glitchIntensity = 1f;
        public bool damageNumbers = true;
        public bool fullscreen = true;
    }

    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 2;
        public const int MaxWeaponLevel = int.MaxValue / 300;

        public int version = CurrentVersion;
        public int yen = 600;
        public int reputation;
        public int spiritAsh;
        public int weaponLevel = 1;
        public int completedRuns;
        public int contractRunSequence;
        public CombatMode combatMode = CombatMode.Legacy;
        public BattleCheckpoint battleCheckpoint;
        public List<string> claimedEconomicRunIds = new List<string>();
        public List<string> firstClearRewardIds = new List<string>();
        public RunPhase phase = RunPhase.Home;
        public string activeContractId = string.Empty;
        public string activeDestinationId = string.Empty;
        public string preparedFoodId = string.Empty;
        public string storedFoodId = string.Empty;
        public int storedFoodCount;
        public float enemyHp;
        public float playerHp = 100f;
        public float enemyTimer;
        public float autoTimer;
        public float clickTimer;
        public float combatTime;
        public bool weakPointClaimed;
        public bool lampOn = true;
        public int mameInteractions;
        public bool hubGuildIntroSeen;
        public MessageSaveData messages = new MessageSaveData();
        public SettingsData settings = new SettingsData();
    }
}
