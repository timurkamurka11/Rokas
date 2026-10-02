using NUnit.Framework;
using Rokas.Core;
using Rokas.Presentation;

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
