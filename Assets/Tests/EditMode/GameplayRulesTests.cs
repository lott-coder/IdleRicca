using IdleRicca.Application.Gameplay;
using NUnit.Framework;

namespace IdleRicca.Tests
{
    public sealed class GameplayRulesTests
    {
        [Test]
        public void MovementFollowsCameraAndCapsDiagonalMagnitude()
        {
            var player = CreatePlayer();
            var forward = player.GetMovement(0f, 1f, 90f, false);
            Assert.That(forward.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(forward.Z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(forward.IsRunning, Is.False);

            var diagonal = player.GetMovement(1f, 1f, 0f, true);
            Assert.That(diagonal.X, Is.EqualTo(diagonal.Z).Within(0.0001f));
            Assert.That(diagonal.X * diagonal.X + diagonal.Z * diagonal.Z, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(diagonal.IsRunning, Is.True);
            Assert.That(player.GetMovement(0f, 0f, 0f, true).IsRunning, Is.False);
        }

        [Test]
        public void InvalidAxisAndYawAreIgnoredBeforeMovement()
        {
            var move = CreatePlayer().GetMovement(float.NaN, float.PositiveInfinity, float.NegativeInfinity, true);
            Assert.That(move.IsMoving, Is.False);
        }

        [Test]
        public void FirstSampleCanCrossEntireWindowAndCompletionWaitsForHits()
        {
            var player = CreatePlayer();
            var target = new EnemyHealth(100);
            var commands = player.Commands;
            Assert.That(commands.TryStartAttack(), Is.True);
            Assert.That(commands.TryStartAttack(), Is.False);
            Assert.That(player.GetMovement(0f, 1f, 0f, true).IsMoving, Is.False);

            var frame = commands.BeginAttackFrame(true, 1.2f, 0.1f);
            Assert.That(frame.IsValid && frame.CanHit, Is.True);
            Assert.That(frame.Start, Is.EqualTo(0.2f));
            Assert.That(frame.End, Is.EqualTo(0.32f));
            Assert.That(frame.PreviousProgress, Is.Zero);
            Assert.That(frame.Progress, Is.EqualTo(1f));
            Assert.That(player.IsAttacking, Is.True);
            Assert.That(commands.TryHit(frame, target), Is.EqualTo(AttackHit.Damaged));
            Assert.That(commands.EndAttackFrame(frame), Is.True);
            Assert.That(commands.TryHit(frame, target), Is.EqualTo(AttackHit.Ignored));
            Assert.That(target.CurrentHp, Is.EqualTo(75));
            Assert.That(player.GetMovement(0f, 1f, 0f, true).IsRunning, Is.True);
        }

        [Test]
        public void EachAttackCanHitSeveralTargetsButOnlyOncePerTarget()
        {
            var commands = CreatePlayer().Commands;
            var first = new EnemyHealth(100);
            var second = new EnemyHealth(60);
            commands.TryStartAttack();
            var early = commands.BeginAttackFrame(true, 0.21f, 0.1f);
            Assert.That(commands.TryHit(early, first), Is.EqualTo(AttackHit.Damaged));
            Assert.That(commands.TryHit(early, first), Is.EqualTo(AttackHit.Ignored));
            Assert.That(commands.TryHit(early, second), Is.EqualTo(AttackHit.Damaged));
            commands.EndAttackFrame(early);
            var late = commands.BeginAttackFrame(true, 0.4f, 0.1f);
            Assert.That(late.Start, Is.EqualTo(0.21f));
            Assert.That(late.End, Is.EqualTo(0.32f));
            Assert.That(commands.TryHit(late, first), Is.EqualTo(AttackHit.Ignored));
            commands.EndAttackFrame(late);
            var recovery = commands.BeginAttackFrame(true, 1f, 0.1f);
            Assert.That(recovery.IsValid, Is.True);
            Assert.That(recovery.CanHit, Is.False);
            Assert.That(commands.TryHit(recovery, new EnemyHealth(1)), Is.EqualTo(AttackHit.Ignored));
            commands.EndAttackFrame(recovery);

            commands.TryStartAttack();
            var next = commands.BeginAttackFrame(true, 0.25f, 0.1f);
            Assert.That(commands.TryHit(early, first), Is.EqualTo(AttackHit.Ignored));
            Assert.That(commands.TryHit(next, first), Is.EqualTo(AttackHit.Damaged));
            Assert.That(first.CurrentHp, Is.EqualTo(50));
            Assert.That(second.CurrentHp, Is.EqualTo(35));
        }

        [Test]
        public void ConfiguredDamageClampsHealthAtZeroAndDeadTargetsStayDead()
        {
            var commands = new PlayerActions(30, 0f, 0.5f).Commands;
            var target = new EnemyHealth(31);
            commands.TryStartAttack();
            var one = commands.BeginAttackFrame(true, 1f, 0.1f);
            Assert.That(commands.TryHit(one, target), Is.EqualTo(AttackHit.Damaged));
            Assert.That(target.CurrentHp, Is.EqualTo(1));
            commands.EndAttackFrame(one);
            commands.TryStartAttack();
            var two = commands.BeginAttackFrame(true, 1f, 0.1f);
            Assert.That(commands.TryHit(two, target), Is.EqualTo(AttackHit.Defeated));
            Assert.That(target.CurrentHp, Is.Zero);
            Assert.That(target.IsAlive, Is.False);
            Assert.That(commands.TryHit(two, target), Is.EqualTo(AttackHit.Ignored));
            Assert.That(new EnemyHealth(31).CurrentHp, Is.EqualTo(31));
        }

        [Test]
        public void CancelAndMissingAnimationReleaseThePlayer()
        {
            var player = CreatePlayer();
            var commands = player.Commands;
            commands.TryStartAttack();
            Assert.That(commands.BeginAttackFrame(false, 0f, 0.25f).IsValid, Is.False);
            Assert.That(player.IsAttacking, Is.True);
            Assert.That(commands.BeginAttackFrame(false, 0f, 0.25f).IsValid, Is.False);
            Assert.That(player.IsAttacking, Is.False);

            commands.TryStartAttack();
            var frame = commands.BeginAttackFrame(true, 0.25f, 0.02f);
            commands.CancelAttack();
            var enemy = new EnemyHealth(100);
            Assert.That(commands.TryHit(frame, enemy), Is.EqualTo(AttackHit.Ignored));
            Assert.That(player.IsAttacking, Is.False);
            Assert.That(commands.TryStartAttack(), Is.True);
        }

        private static PlayerActions CreatePlayer() => new PlayerActions(25, 0.2f, 0.32f);
    }
}
