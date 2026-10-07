using System;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;

namespace Rokas.Tests
{
    public sealed class UnitySaveTests
    {
        private string directory;
        [SetUp] public void Setup() { directory = Path.Combine(Path.GetTempPath(), "rokas-codec-" + Guid.NewGuid().ToString("N")); }
        [TearDown] public void Teardown() { if (Directory.Exists(directory)) Directory.Delete(directory, true); }

        [Test]
        public void UnityJsonRestoresAcceptedContractAndPendingPayment()
        {
            var store = new SaveStore(directory, new UnitySaveCodec());
            var state = new SaveData { activeContractId = "contract_subway_001", phase = RunPhase.Accepted, preparedFoodId = "food_green_tea", yen = 520 };
            Assert.That(store.Save(state).Succeeded, Is.True);
            var restored = store.Load();
            Assert.That(restored.Succeeded, Is.True);
            Assert.That(restored.Data.phase, Is.EqualTo(RunPhase.Accepted));
            Assert.That(restored.Data.preparedFoodId, Is.EqualTo("food_green_tea"));
            Assert.That(restored.Data.yen, Is.EqualTo(520));
            state.phase = RunPhase.Payment;
            Assert.That(store.Save(state).Succeeded, Is.True);
            var session = new GameSession(store.Load().Data, new ContractDefinition());
            Assert.That(session.ClaimPayment(), Is.True);
            Assert.That(store.Save(session.State).Succeeded, Is.True);
            var reopened = new GameSession(store.Load().Data, new ContractDefinition());
            Assert.That(reopened.ClaimPayment(), Is.False);
        }

        [Test]
        public void PartialJsonCannotReplaceAValidBackup()
        {
            var store = new SaveStore(directory, new UnitySaveCodec());
            var state = new SaveData { yen = 1900 };
            Assert.That(store.Save(state).Succeeded, Is.True);
            state.yen = 2600;
            Assert.That(store.Save(state).Succeeded, Is.True);
            File.WriteAllText(store.PrimaryPath, "{\"version\":1}");
            var recovered = store.Load();
            Assert.That(recovered.Status, Is.EqualTo(SaveLoadStatus.RecoveredBackup));
            Assert.That(recovered.Data.yen, Is.EqualTo(1900));
        }

        [Test]
        public void HallwayLightPersistsIndependentlyFromMainRoomLight()
        {
            var store = new SaveStore(directory, new UnitySaveCodec());
            var state = new SaveData
            {
                lampOn = true,
                hallwayLightState = 2,
                yen = 1200
            };

            Assert.That(store.Save(state).Succeeded, Is.True);
            SaveLoadResult loaded = store.Load();
            Assert.That(loaded.Succeeded, Is.True);

            var session = new GameSession(loaded.Data, new ContractDefinition());
            Assert.That(session.State.lampOn, Is.True);
            Assert.That(session.HallwayLightOn, Is.False);

            session.SetLamp(false);
            session.SetHallwayLight(true);
            Assert.That(store.Save(session.State).Succeeded, Is.True);

            var reopened = new GameSession(store.Load().Data, new ContractDefinition());
            Assert.That(reopened.State.lampOn, Is.False);
            Assert.That(reopened.HallwayLightOn, Is.True);
        }

        [Test]
        public void LegacySaveWithoutHallwayLightDefaultsToOn()
        {
            var legacy = new SaveData
            {
                lampOn = false,
                hallwayLightState = 0
            };

            var session = new GameSession(legacy, new ContractDefinition());
            Assert.That(session.State.lampOn, Is.False,
                "MainRoomLight migration must not alter the existing room state.");
            Assert.That(session.HallwayLightOn, Is.True);
            Assert.That(session.State.hallwayLightState, Is.EqualTo(1),
                "Missing/zero Hallway state must normalize to the safe ON default.");
        }
    }
}
