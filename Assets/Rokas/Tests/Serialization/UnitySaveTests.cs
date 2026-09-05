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
    }
}
