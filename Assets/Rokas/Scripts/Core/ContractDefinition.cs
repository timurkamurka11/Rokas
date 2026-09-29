using System;

namespace Rokas.Core
{
    [Serializable]
    public sealed class ContractDefinition
    {
        public string id = "contract_subway_001";
        public string title = "Last Train Home";
        public string location = "Abandoned Subway";
        public string clientNote = "The final train still arrives after the station closes.";
        public int reward = 1800;
        public int reputationReward = 10;
        public int ashReward = 3;
        public string enemyId = "enemy_faceless_commuter";
        public float enemyHealth = 180f;
        public float enemyDamage = 8f;
        public float enemyInterval = 2.6f;
        public float autoInterval = 1.2f;
        public float autoDamage = 8f;
        public float clickDamage = 5f;
    }
}
