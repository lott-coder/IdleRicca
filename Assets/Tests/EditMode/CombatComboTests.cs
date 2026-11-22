using IdleRicca.Application.Gameplay;
using NUnit.Framework;

namespace IdleRicca.Tests.EditMode
{
    public sealed class CombatComboTests
    {
        [TestCase(4.999, 2)]
        [TestCase(5.0, 1)]
        [TestCase(5.001, 1)]
        public void HitWindowRunsFromMostRecentSuccessfulHit(double nextHitAt, int expectedCombo)
        {
            var ricca = CreateRicca();
            var enemy = new EnemyHealth(100);
            Assert.That(ricca.CombatStatus.ComboCount, Is.Zero);

            Hit(ricca, enemy);
            Assert.That(ricca.CombatStatus.ComboCount, Is.EqualTo(1));
            ricca.Commands.AdvanceCombatTime(nextHitAt);
            if (nextHitAt >= 5.0)
            {
                Assert.That(ricca.CombatStatus.ComboCount, Is.Zero);
                Assert.That(ricca.CombatStatus.ComboSecondsRemaining, Is.Zero);
            }

            Hit(ricca, enemy);
            Assert.That(ricca.CombatStatus.ComboCount, Is.EqualTo(expectedCombo));
            Assert.That(ricca.CombatStatus.ComboSecondsRemaining, Is.EqualTo(5.0));
            Assert.That(ricca.CombatStatus.HitSequence, Is.EqualTo(2UL));
        }

        [Test]
        public void EmptyAndRepeatedContactsDoNotProlongAnExistingCombo()
        {
            var ricca = CreateRicca();
            var enemy = new EnemyHealth(100);

            var empty = StartSwing(ricca);
            Assert.That(ricca.Commands.TryHit(empty, null), Is.EqualTo(AttackHit.Ignored));
            ricca.Commands.EndAttackFrame(empty);
            Assert.That(ricca.CombatStatus.ComboCount, Is.Zero);

            var first = StartSwing(ricca);
            Assert.That(ricca.Commands.TryHit(first, enemy), Is.EqualTo(AttackHit.Damaged));
            ricca.Commands.AdvanceCombatTime(2);
            Assert.That(ricca.Commands.TryHit(first, enemy), Is.EqualTo(AttackHit.Ignored));
            ricca.Commands.EndAttackFrame(first);

            empty = StartSwing(ricca);
            Assert.That(ricca.Commands.TryHit(empty, null), Is.EqualTo(AttackHit.Ignored));
            ricca.Commands.EndAttackFrame(empty);
            Assert.That(ricca.CombatStatus.ComboCount, Is.EqualTo(1));
            Assert.That(ricca.CombatStatus.ComboSecondsRemaining, Is.EqualTo(3));
            Assert.That(ricca.CombatStatus.HitSequence, Is.EqualTo(1UL));

            ricca.Commands.AdvanceCombatTime(5);
            Assert.That(ricca.CombatStatus.ComboCount, Is.Zero);
        }

        [Test]
        public void DifferentTargetsAndADeathEachCountOncePerSwing()
        {
            var ricca = CreateRicca();
            var first = new EnemyHealth(100);
            var second = new EnemyHealth(25);
            var sample = StartSwing(ricca);

            Assert.That(ricca.Commands.TryHit(sample, first), Is.EqualTo(AttackHit.Damaged));
            Assert.That(ricca.Commands.TryHit(sample, first), Is.EqualTo(AttackHit.Ignored));
            Assert.That(ricca.Commands.TryHit(sample, second), Is.EqualTo(AttackHit.Defeated));
            Assert.That(ricca.Commands.TryHit(sample, second), Is.EqualTo(AttackHit.Ignored));
            ricca.Commands.EndAttackFrame(sample);

            Assert.That(ricca.CombatStatus.ComboCount, Is.EqualTo(2));
            Assert.That(ricca.CombatStatus.ComboSecondsRemaining, Is.EqualTo(5));
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(double.NegativeInfinity)]
        [TestCase(-1)]
        [TestCase(11)]
        public void InvalidOrBackwardsTimeCannotChangeTheWindow(double invalidTime)
        {
            var ricca = CreateRicca();
            ricca.Commands.AdvanceCombatTime(10);
            Hit(ricca, new EnemyHealth(100));
            ricca.Commands.AdvanceCombatTime(12);
            ricca.Commands.AdvanceCombatTime(invalidTime);

            Assert.That(ricca.CombatStatus.ComboCount, Is.EqualTo(1));
            Assert.That(ricca.CombatStatus.ComboSecondsRemaining, Is.EqualTo(3));
            ricca.Commands.AdvanceCombatTime(15);
            Assert.That(ricca.CombatStatus.ComboCount, Is.Zero);
        }

        [TestCase(100, 90, AttackHit.Damaged)]
        [TestCase(5, 0, AttackHit.Defeated)]
        public void OnlyActualEnemyDamageClearsTheCombo(int initialHp, int expectedHp, AttackHit result)
        {
            var ricca = CreateRicca(initialHp);
            var target = new EnemyHealth(100);
            var enemy = new EnemyActions(new EnemyHealth(100), 1.25f, .75f, 10, .3f, .5f, 1f, 1f, 1f, 1f);
            Assert.That(enemy.Commands.BindTarget(ricca), Is.True);
            Hit(ricca, target);
            Hit(ricca, target);
            Assert.That(ricca.CombatStatus.ComboCount, Is.EqualTo(2));

            enemy.Commands.Step(true, 1f, 0f, true, .1f);
            var windup = enemy.Commands.BeginAttackFrame(true, .1f, .1f);
            Assert.That(enemy.Commands.TryHitRicca(windup, ricca), Is.EqualTo(AttackHit.Ignored));
            enemy.Commands.EndAttackFrame(windup);
            Assert.That(ricca.CombatStatus.ComboCount, Is.EqualTo(2));
            Assert.That(ricca.CombatStatus.CurrentHp, Is.EqualTo(initialHp));

            var active = enemy.Commands.BeginAttackFrame(true, .4f, .1f);
            Assert.That(enemy.Commands.TryHitRicca(active, ricca), Is.EqualTo(result));
            Assert.That(ricca.CombatStatus.CurrentHp, Is.EqualTo(expectedHp));
            Assert.That(ricca.CombatStatus.MaxHp, Is.EqualTo(initialHp));
            Assert.That(ricca.CombatStatus.ComboCount, Is.Zero);
            Assert.That(ricca.CombatStatus.ComboSecondsRemaining, Is.Zero);

            if (ricca.IsAlive)
            {
                Hit(ricca, target);
                Assert.That(ricca.CombatStatus.ComboCount, Is.EqualTo(1));
            }
        }

        private static PlayerActions CreateRicca(int hp = 100) => new PlayerActions(25, .2f, .5f, startingHp: hp);

        private static AttackSampleBatch StartSwing(PlayerActions ricca)
        {
            Assert.That(ricca.Commands.TryStartAttack(), Is.True);
            return ricca.Commands.BeginAttackFrame(true, 1f, .1f);
        }

        private static void Hit(PlayerActions ricca, EnemyHealth target)
        {
            var swing = StartSwing(ricca);
            Assert.That(ricca.Commands.TryHit(swing, target), Is.Not.EqualTo(AttackHit.Ignored));
            ricca.Commands.EndAttackFrame(swing);
        }
    }
}
