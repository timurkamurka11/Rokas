using System;
using System.IO;
using NUnit.Framework;
using Rokas.Core;
using Rokas.Core.ReactiveTurns;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Core.Tests
{
    public sealed class ReactiveUnitySaveCodecTests
    {
        [Test]
        public void ActiveReactiveCheckpointRoundTripsThroughUnityJsonAndResumesOnce()
        {
            string directory = Path.Combine(Path.GetTempPath(), "rokas-reactive-codec-" + Guid.NewGuid().ToString("N"));
            try
            {
                RokasAssets assets = Resources.Load<RokasAssets>("RokasAssets");
                Assert.That(assets, Is.Not.Null);
                ContractDefinition contract = JsonUtility.FromJson<ContractDefinition>(assets.contract.text);
                var store = new SaveStore(directory, new UnitySaveCodec());
                var session = new GameSession(new SaveData(), contract, store);
                Assert.That(session.AcceptContract(), Is.True);
                Assert.That(session.LeaveHome(), Is.True);
                Assert.That(session.EnterReactiveTestEncounter(), Is.True);
                Assert.That(session.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
                Assert.That(session.SaveReactiveCheckpoint(), Is.True);

                SaveLoadResult loaded = store.Load();
                Assert.That(loaded.Succeeded, Is.True, loaded.Message);
                Assert.That(loaded.Data.combatMode, Is.EqualTo(CombatMode.ReactiveTurns));
                Assert.That(loaded.Data.battleCheckpoint, Is.Not.Null);
                Assert.That(loaded.Data.battleCheckpoint.contentHash, Is.Not.Empty);
                Assert.That(loaded.Data.battleCheckpoint.actors.Length, Is.EqualTo(9));
                Assert.That(loaded.Data.battleCheckpoint.queue.Length, Is.EqualTo(9));
                Assert.That(loaded.Data.battleCheckpoint.waveIndex, Is.EqualTo(0));
                Assert.That(loaded.Data.battleCheckpoint.waveEntry, Is.Not.Null);
                Assert.That(loaded.Data.battleCheckpoint.selectedTargetId, Is.EqualTo("E1"));
                Assert.That(loaded.Data.battleCheckpoint.hunterAp, Is.EqualTo(4));

                var restored = new GameSession(loaded.Data, contract, store);
                Assert.That(restored.ReactiveCombat.Phase, Is.EqualTo(ReactivePhase.PlayerCommand));
                Assert.That(restored.ReactiveCombat.HunterAp, Is.EqualTo(4),
                    "A stable command turn must not grant its AP again on reload.");
                Assert.That(restored.ReactiveCombat.ActiveEnemyIds.Count, Is.EqualTo(3));
                Assert.That(restored.ReactiveCombat.SelectedTargetId, Is.EqualTo("E1"));
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
