using System.IO;
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
                Assert.That(sword.name, Does.Contain("PremiumGreatsword"));
                Assert.That(sword.vertexCount, Is.GreaterThan(1700),
                    "The final two-hander must retain enough geometry for bevels, guard, wraps and pommel detail.");
                Assert.That(sword.triangles.Length / 3, Is.GreaterThan(700));
                Assert.That(sword.subMeshCount, Is.EqualTo(3),
                    "Blade, accent steel and grip must retain separate material regions.");
                Assert.That(sword.bounds.size.y, Is.GreaterThan(sword.bounds.size.x * 2.2f),
                    "The broad greatsword must still read as a long two-handed weapon rather than a slab.");
                Assert.That(sword.bounds.size.x, Is.GreaterThan(1.0f),
                    "The final silhouette must remain visibly broad/heavy rather than collapsing back to a generic longsword.");
                Assert.That(sword.bounds.size.z, Is.LessThan(.22f));
                Assert.That(sword.uv, Has.Length.EqualTo(sword.vertexCount),
                    "Premium material detail requires stable planar UVs on the runtime sword mesh.");
                Assert.That(sword.tangents, Has.Length.EqualTo(sword.vertexCount),
                    "Runtime sword tangents must be rebuilt for the brushed normal detail.");
                Assert.That(dagger.name, Does.Contain("PremiumGreatswordSet"));
                Assert.That(dagger.vertexCount, Is.GreaterThan(1200));
                Assert.That(dagger.triangles.Length / 3, Is.GreaterThan(500));
                Assert.That(dagger.subMeshCount, Is.EqualTo(3),
                    "The matching dagger keeps blade, accent steel and grip material regions.");
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
        public void LaptopOnlyWorkbenchRouteRemovesLegacyHubEntry()
        {
            string home = File.ReadAllText("Assets/Rokas/Scripts/Presentation/HomeView.cs");
            string view = File.ReadAllText("Assets/Rokas/Scripts/Presentation/RokasView.cs");
            string panels = File.ReadAllText("Assets/Rokas/Scripts/Presentation/ContractPanels.cs");

            Assert.That(home, Does.Not.Contain("WorkbenchHotspot"));
            Assert.That(home, Does.Not.Contain("open(\"workbench\")"));
            Assert.That(view, Does.Contain("Workbench is laptop-only"));
            Assert.That(panels, Does.Not.Contain("WorkbenchModal"));
            Assert.That(panels, Does.Contain("BuildLaptopWorkbenchPage"));
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
