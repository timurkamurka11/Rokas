using System;

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
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public int yen = 600;
        public int reputation;
        public int spiritAsh;
        public int weaponLevel = 1;
        public int completedRuns;
        public RunPhase phase = RunPhase.Home;
        public string activeContractId = string.Empty;
        public string preparedFoodId = string.Empty;
        public float enemyHp;
        public float playerHp = 100f;
        public float enemyTimer;
        public float autoTimer;
        public float clickTimer;
        public float combatTime;
        public bool weakPointClaimed;
        public bool lampOn = true;
        public int mameInteractions;
        public SettingsData settings = new SettingsData();
    }
}
