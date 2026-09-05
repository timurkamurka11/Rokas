using NUnit.Framework;

namespace Rokas.Core.Tests
{
    public sealed class FirstLoopDomainTests
    {
        [Test]
        public void ContractRewardCannotBeClaimedTwice()
        {
            ContractDefinition contract = new ContractDefinition();
            contract.enemyHealth = contract.clickDamage;
            GameSession session = new GameSession(new SaveData(), contract);

            Assert.That(session.AcceptContract(), Is.True);
            Assert.That(session.LeaveHome(), Is.True);
            Assert.That(session.EnterPortal(), Is.True);
            Assert.That(session.ClickAttack(false), Is.True);
            Assert.That(session.ReturnHome(), Is.True);
            Assert.That(session.ClaimPayment(), Is.True);
            Assert.That(session.ClaimPayment(), Is.False);
            Assert.That(session.State.yen, Is.EqualTo(2400));
            Assert.That(session.State.spiritAsh, Is.EqualTo(3));
        }

        [Test]
        public void HomeCannotSkipPhaseGuardIntoCombat()
        {
            ContractDefinition contract = new ContractDefinition();
            SaveData state = new SaveData();
            state.activeContractId = contract.id;
            GameSession session = new GameSession(state, contract);

            Assert.That(session.EnterPortal(), Is.False);
            Assert.That(session.ClickAttack(false), Is.False);
            Assert.That(session.State.phase, Is.EqualTo(RunPhase.Home));
        }

        [Test]
        public void CombatTimersResumeFromSavedValues()
        {
            ContractDefinition contract = new ContractDefinition();
            SaveData state = new SaveData();
            state.phase = RunPhase.Combat;
            state.activeContractId = contract.id;
            state.enemyHp = 91f;
            state.playerHp = 76f;
            state.autoTimer = .2f;
            state.enemyTimer = 1f;
            GameSession session = new GameSession(state, contract);

            session.Tick(.2f);

            Assert.That(session.State.enemyHp, Is.EqualTo(83f).Within(.001f));
            Assert.That(session.State.playerHp, Is.EqualTo(76f));
        }
    }
}
