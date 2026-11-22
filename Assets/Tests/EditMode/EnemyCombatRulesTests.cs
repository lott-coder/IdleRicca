using IdleRicca.Application.Gameplay;
using NUnit.Framework;

namespace IdleRicca.Tests
{
    public sealed class EnemyCombatRulesTests
    {
        [Test]
        public void ChaseRequiresPathAndAttackRequiresRangeHeightAndSight()
        {
            var player = new PlayerActions(25, .2f, .5f);
            var enemy = CreateEnemy(player);
            var commands = enemy.Commands;
            Assert.That(commands.Step(false, 100f, 0f, false, .1f), Is.EqualTo(EnemyPhase.Idle));
            Assert.That(commands.Step(true, 100f, 0f, true, .1f), Is.EqualTo(EnemyPhase.Chasing));
            Assert.That(commands.Step(true, 1f, 0f, false, .1f), Is.EqualTo(EnemyPhase.Chasing));
            Assert.That(commands.Step(true, 1f, 2f, true, .1f), Is.EqualTo(EnemyPhase.Chasing));
            Assert.That(commands.Step(true, float.NaN, 0f, true, .1f), Is.EqualTo(EnemyPhase.Idle));
            Assert.That(commands.Step(true, 1f, 0f, true, .1f), Is.EqualTo(EnemyPhase.Attacking));
        }

        [Test]
        public void SkippedWindowStillHitsOnlyBoundRiccaOnceAndStartsCooldownOnCompletion()
        {
            var player = new PlayerActions(25, .2f, .5f);
            var other = new PlayerActions(25, .2f, .5f);
            var commands = CreateEnemy(player).Commands;
            Assert.That(commands.Step(true, 1f, 0f, true, .1f), Is.EqualTo(EnemyPhase.Attacking));
            var windup = commands.BeginAttackFrame(true, .1f, .1f);
            Assert.That(windup.CanHit, Is.False);
            Assert.That(commands.TryHitRicca(windup, player), Is.EqualTo(AttackHit.Ignored));
            Assert.That(commands.EndAttackFrame(windup), Is.False);

            var skipped = commands.BeginAttackFrame(true, 1.1f, .1f);
            Assert.That(skipped.CanHit, Is.True);
            Assert.That(skipped.Progress, Is.EqualTo(1.1f));
            Assert.That(skipped.Start, Is.EqualTo(.3f));
            Assert.That(skipped.End, Is.EqualTo(.5f));
            Assert.That(commands.TryHitRicca(skipped, other), Is.EqualTo(AttackHit.Ignored));
            Assert.That(other.CurrentHp, Is.EqualTo(100));
            Assert.That(commands.TryHitRicca(skipped, player), Is.EqualTo(AttackHit.Damaged));
            Assert.That(commands.TryHitRicca(skipped, player), Is.EqualTo(AttackHit.Ignored));
            Assert.That(player.CurrentHp, Is.EqualTo(90));
            Assert.That(commands.EndAttackFrame(skipped), Is.True);
            Assert.That(commands.TryHitRicca(skipped, player), Is.EqualTo(AttackHit.Ignored));
            Assert.That(commands.Step(true, 1f, 0f, true, .75f), Is.EqualTo(EnemyPhase.Idle));
            Assert.That(commands.Step(true, 1f, 0f, true, .26f), Is.EqualTo(EnemyPhase.Attacking));
        }

        [Test]
        public void DamageInvalidatesOpenAttackAndDeathCannotReturnToCombat()
        {
            var ricca = new PlayerActions(50, .2f, .5f);
            var enemy = CreateEnemy(ricca);
            var commands = enemy.Commands;
            commands.Step(true, 1f, 0f, true, .1f);
            var stale = commands.BeginAttackFrame(true, .4f, .1f);
            DamageEnemy(ricca, enemy.Health);
            Assert.That(commands.ObserveHit(), Is.EqualTo(EnemyPhase.Hit));
            Assert.That(commands.TryHitRicca(stale, ricca), Is.EqualTo(AttackHit.Ignored));
            Assert.That(ricca.CurrentHp, Is.EqualTo(100));
            DamageEnemy(ricca, enemy.Health);
            Assert.That(commands.ObserveHit(), Is.EqualTo(EnemyPhase.Death));
            Assert.That(commands.ObserveAnimation(EnemyPhase.Death, .9f, .2f), Is.EqualTo(EnemyPhase.Death));
            Assert.That(commands.ObserveAnimation(EnemyPhase.Death, 1f, .2f), Is.EqualTo(EnemyPhase.Retired));
            Assert.That(commands.Step(true, 1f, 0f, true, 10f), Is.EqualTo(EnemyPhase.Retired));
        }

        [Test]
        public void DamageSequenceOnlyAdvancesWhenRiccaLosesHpAndCancelsHerSwing()
        {
            var ricca = new PlayerActions(25, .2f, .5f);
            var enemy = CreateEnemy(ricca);
            Assert.That(ricca.Commands.TryStartAttack(), Is.True);
            var sword = ricca.Commands.BeginAttackFrame(true, .4f, .1f);
            enemy.Commands.Step(true, 1f, 0f, true, .1f);

            var windup = enemy.Commands.BeginAttackFrame(true, .2f, .1f);
            Assert.That(enemy.Commands.TryHitRicca(windup, ricca), Is.EqualTo(AttackHit.Ignored));
            Assert.That(ricca.DamageSequence, Is.Zero);
            Assert.That(ricca.IsAttacking, Is.True);
            enemy.Commands.EndAttackFrame(windup);

            var strike = enemy.Commands.BeginAttackFrame(true, .4f, .1f);
            Assert.That(enemy.Commands.TryHitRicca(strike, ricca), Is.EqualTo(AttackHit.Damaged));
            Assert.That(ricca.DamageSequence, Is.EqualTo(1UL));
            Assert.That(ricca.IsAttacking, Is.False);
            Assert.That(ricca.Commands.TryHit(sword, enemy.Health), Is.EqualTo(AttackHit.Ignored));
            Assert.That(enemy.Commands.TryHitRicca(strike, ricca), Is.EqualTo(AttackHit.Ignored));
            Assert.That(ricca.DamageSequence, Is.EqualTo(1UL));
        }

        [Test]
        public void LethalEnemyAttackCancelsRiccaMovementAndPendingSwing()
        {
            var ricca = new PlayerActions(25, .2f, .5f, startingHp: 5);
            var enemy = CreateEnemy(ricca);
            Assert.That(ricca.Commands.TryStartAttack(), Is.True);
            var pending = ricca.Commands.BeginAttackFrame(true, .4f, .1f);
            enemy.Commands.Step(true, 1f, 0f, true, .1f);
            var attack = enemy.Commands.BeginAttackFrame(true, .4f, .1f);

            Assert.That(enemy.Commands.TryHitRicca(attack, ricca), Is.EqualTo(AttackHit.Defeated));
            Assert.That(ricca.CurrentHp, Is.Zero);
            Assert.That(ricca.IsAlive || ricca.IsAttacking, Is.False);
            Assert.That(ricca.Commands.TryStartAttack(), Is.False);
            Assert.That(ricca.GetMovement(0f, 1f, 0f, true).IsMoving, Is.False);
            Assert.That(ricca.Commands.TryHit(pending, enemy.Health), Is.EqualTo(AttackHit.Ignored));
            Assert.That(enemy.Health.CurrentHp, Is.EqualTo(100));
        }

        [Test]
        public void StuckAnimationCannotRetainAttackOrDeathIndefinitely()
        {
            var ricca = new PlayerActions(100, .2f, .5f);
            var enemy = CreateEnemy(ricca);
            var commands = enemy.Commands;
            commands.Step(true, 1f, 0f, true, .1f);
            var opening = commands.BeginAttackFrame(true, .1f, .1f);
            commands.EndAttackFrame(opening);
            Assert.That(commands.BeginAttackFrame(true, .1f, 2f).IsValid, Is.False);
            Assert.That(enemy.Phase, Is.EqualTo(EnemyPhase.Idle));
            DamageEnemy(ricca, enemy.Health);
            Assert.That(commands.ObserveHit(), Is.EqualTo(EnemyPhase.Death));
            Assert.That(commands.ObserveAnimation(EnemyPhase.Idle, 0f, 2f), Is.EqualTo(EnemyPhase.Retired));
        }

        private static EnemyActions CreateEnemy(PlayerActions player)
        {
            var enemy = new EnemyActions(new EnemyHealth(100), 1.25f, .75f, 10, .3f, .5f, 1f, 1f, 1f, 1f);
            enemy.Commands.BindTarget(player);
            return enemy;
        }

        private static void DamageEnemy(PlayerActions ricca, EnemyHealth enemy)
        {
            ricca.Commands.TryStartAttack();
            var batch = ricca.Commands.BeginAttackFrame(true, 1f, .1f);
            ricca.Commands.TryHit(batch, enemy);
            ricca.Commands.EndAttackFrame(batch);
        }
    }
}
