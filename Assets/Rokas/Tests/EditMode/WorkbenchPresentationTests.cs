using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;
using UnityEngine;

namespace Rokas.Tests
{
    public sealed class WorkbenchPresentationTests
    {
        [Test]
        public void TwoHandedPresentationUsesAuthoritativeEconomyValues()
        {
            var state = new SaveData
            {
                phase = RunPhase.Home,
                weaponLevel = 3,
                yen = 5000
            };
            var session = new GameSession(state, new ContractDefinition());

            WorkbenchWeaponViewData data = WorkbenchPresentationModel.Create(session, WorkbenchWeaponKind.TwoHanded);

            Assert.That(data.Category, Is.EqualTo("ДВУРУЧНИК"));
            Assert.That(data.LevelText, Does.Contain("3"));
            Assert.That(data.StatLine, Does.Contain("+40%"));
            Assert.That(data.UpgradeCost, Is.EqualTo(900));
            Assert.That(data.UpgradeButtonText, Does.Contain("900"));
            Assert.That(data.CanUpgrade, Is.True);
            Assert.That(data.Tiers[0].State, Is.EqualTo(WorkbenchTierState.Current));
            Assert.That(data.Tiers[1].State, Is.EqualTo(WorkbenchTierState.Available));
            Assert.That(data.Tiers[2].State, Is.EqualTo(WorkbenchTierState.Locked));
        }

        [Test]
        public void InsufficientCurrencyDisablesRealUpgradeWithoutChangingCost()
        {
            var state = new SaveData
            {
                phase = RunPhase.Home,
                weaponLevel = 2,
                yen = 50
            };
            var session = new GameSession(state, new ContractDefinition());

            WorkbenchWeaponViewData data = WorkbenchPresentationModel.Create(session, WorkbenchWeaponKind.TwoHanded);

            Assert.That(data.UpgradeCost, Is.EqualTo(600));
            Assert.That(data.CanUpgrade, Is.False);
            Assert.That(data.LockReason, Does.Contain("НЕДОСТАТОЧНО"));
        }

        [Test]
        public void DaggerPresentationDoesNotInventGameplayEconomy()
        {
            var session = new GameSession(new SaveData(), new ContractDefinition());

            WorkbenchWeaponViewData data = WorkbenchPresentationModel.Create(session, WorkbenchWeaponKind.Dagger);

            Assert.That(data.Category, Is.EqualTo("КИНЖАЛ"));
            Assert.That(data.UpgradeCost, Is.EqualTo(-1));
            Assert.That(data.CanUpgrade, Is.False);
            Assert.That(data.UpgradeButtonText, Does.Contain("НЕДОСТУПНО"));
            Assert.That(data.Tiers, Has.Length.EqualTo(3));
            Assert.That(data.Tiers[0].State, Is.EqualTo(WorkbenchTierState.Locked));
            Assert.That(data.Tiers[1].State, Is.EqualTo(WorkbenchTierState.Locked));
            Assert.That(data.Tiers[2].State, Is.EqualTo(WorkbenchTierState.Locked));
        }

        [Test]
        public void ThreeDimensionalWeaponMeshesDecodeWithExpectedSourceShape()
        {
            Mesh sword = WorkbenchWeaponMeshLibrary.Create(WorkbenchWeaponKind.TwoHanded);
            Mesh dagger = WorkbenchWeaponMeshLibrary.Create(WorkbenchWeaponKind.Dagger);
            try
            {
                Assert.That(WorkbenchWeaponMeshLibrary.SuppliedFbxOriginalVertexCount, Is.EqualTo(48858));
                Assert.That(WorkbenchWeaponMeshLibrary.SuppliedFbxOriginalPolygonCount, Is.EqualTo(97712));
                Assert.That(sword.name, Does.Contain("FbxDerived"));
                Assert.That(sword.vertexCount, Is.GreaterThanOrEqualTo(500),
                    "The FBX-derived preview must keep a non-trivial optimized 3D silhouette.");
                Assert.That(sword.triangles.Length / 3, Is.GreaterThan(900),
                    "The optimized preview must retain enough surface detail for the central viewer.");
                Assert.That(sword.bounds.size.y, Is.GreaterThan(sword.bounds.size.z * 8f),
                    "The supplied sword preview must preserve the long thin FBX silhouette.");
                Assert.That(dagger.name, Does.Contain("RitualDagger"));
                Assert.That(dagger.vertexCount, Is.GreaterThan(150));
                Assert.That(dagger.subMeshCount, Is.EqualTo(2),
                    "The authored dagger keeps separate metal and grip material regions.");
                Assert.That(dagger.bounds.size.y, Is.GreaterThan(dagger.bounds.size.x));
            }
            finally
            {
                Object.DestroyImmediate(sword);
                Object.DestroyImmediate(dagger);
            }
        }

        [Test]
        public void RussianPresentationStringsContainNoKnownMojibakeMarkers()
        {
            var session = new GameSession(new SaveData(), new ContractDefinition());
            foreach (WorkbenchWeaponKind kind in new[] { WorkbenchWeaponKind.TwoHanded, WorkbenchWeaponKind.Dagger })
            {
                WorkbenchWeaponViewData data = WorkbenchPresentationModel.Create(session, kind);
                string combined = string.Join(" ", data.Category, data.Title, data.LevelText, data.Description,
                    data.StatLine, data.UpgradeButtonText, data.LockReason);
                Assert.That(combined, Does.Not.Contain("Рџ"));
                Assert.That(combined, Does.Not.Contain("РЎ"));
                Assert.That(combined, Does.Not.Contain("Рµ"));
            }
        }
    }
}
