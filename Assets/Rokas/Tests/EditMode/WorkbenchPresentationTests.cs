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
                Assert.That(sword.name, Does.Contain("PremiumAuthored"));
                Assert.That(sword.vertexCount, Is.EqualTo(1270),
                    "The final Workbench sword must use the authored hero presentation mesh.");
                Assert.That(sword.triangles.Length / 3, Is.EqualTo(524));
                Assert.That(sword.subMeshCount, Is.EqualTo(3),
                    "Blade, accent steel and grip must retain separate material regions.");
                Assert.That(sword.bounds.size.y, Is.GreaterThan(sword.bounds.size.x * 2.5f),
                    "Premium two-handed silhouette must stay long and confidently narrow.");
                Assert.That(sword.bounds.size.z, Is.LessThan(.20f));
                Assert.That(sword.uv, Has.Length.EqualTo(sword.vertexCount),
                    "Premium material detail requires stable planar UVs on the runtime sword mesh.");
                Assert.That(sword.tangents, Has.Length.EqualTo(sword.vertexCount),
                    "Runtime sword tangents must be rebuilt for the brushed normal detail.");
                Assert.That(dagger.name, Does.Contain("RitualDagger"));
                Assert.That(dagger.vertexCount, Is.GreaterThan(150));
                Assert.That(dagger.subMeshCount, Is.EqualTo(2),
                    "The authored dagger keeps separate metal and grip material regions.");
                Assert.That(dagger.bounds.size.y, Is.GreaterThan(dagger.bounds.size.x));
                Assert.That(dagger.uv, Has.Length.EqualTo(dagger.vertexCount));
                Assert.That(dagger.tangents, Has.Length.EqualTo(dagger.vertexCount));
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
