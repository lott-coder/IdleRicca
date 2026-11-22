using IdleRicca.Application.Gameplay;
using IdleRicca.Domain.Gameplay;
using NUnit.Framework;

namespace IdleRicca.Tests
{
    public sealed class PlayerSkillTests
    {
        [Test]
        public void EmptyMenuBlocksCombatInputsButDoesNotProvideBulletTime()
        {
            var player = SkillWaveRewardTests.NewPlayer();
            var skills = player.SkillCommands;
            Assert.That(skills.TryOpenSkillSelection(), Is.True);
            SkillStatus status = player.SkillStatus;
            Assert.That(status.IsSelecting, Is.True);
            Assert.That(status.IsUsingBulletTime, Is.False);
            Assert.That(skills.TryCastSkill(status.SelectionId, 0, 0d, out _), Is.False);
            Assert.That(player.Commands.TryStartAttack(), Is.False);
            Assert.That(player.GetMovement(1f, 0f, 0f, false).IsMoving, Is.False);

            Assert.That(skills.CancelSkillSelection(status.SelectionId), Is.True);
            Assert.That(skills.CancelSkillSelection(status.SelectionId), Is.False);
            Assert.That(player.Commands.TryStartAttack(), Is.True);
            Assert.That(skills.TryOpenSkillSelection(), Is.False);
        }

        [Test]
        public void AFinalFrameAcrossTheWindowAppliesTheCapturedDamageOncePerEnemy()
        {
            var player = SkillWaveRewardTests.NewPlayer();
            SkillWaveRewardTests.GrantFirstSkill(player, 2);
            int comboBeforeCast = player.CombatStatus.ComboCount;
            SkillCast cast = StartCast(player, 1d);
            var skills = player.SkillCommands;

            Assert.That(player.Commands.TryStartAttack(), Is.False);
            Assert.That(player.Commands.BeginAttackFrame(true, 1f, .02f).IsValid, Is.False);
            Assert.That(skills.TryOpenSkillSelection(), Is.False);
            Assert.That(player.GetMovement(0f, 1f, 0f, false).IsMoving, Is.False);

            var enemy = new EnemyHealth(100);
            var other = new EnemyHealth(100);
            var early = skills.BeginSkillFrame(cast.Id, true, .1f, 1.1d);
            Assert.That(skills.TryHitSkill(early, enemy), Is.EqualTo(AttackHit.Ignored));
            Assert.That(skills.EndSkillFrame(early), Is.False);

            var final = skills.BeginSkillFrame(cast.Id, true, 1.02f, 1.6d);
            Assert.That(final.CanHit, Is.True);
            Assert.That(player.Commands.TryHit(final, enemy), Is.EqualTo(AttackHit.Ignored));
            Assert.That(skills.TryHitSkill(final, enemy), Is.EqualTo(AttackHit.Damaged));
            Assert.That(skills.TryHitSkill(final, enemy), Is.EqualTo(AttackHit.Ignored));
            Assert.That(skills.TryHitSkill(final, other), Is.EqualTo(AttackHit.Damaged));
            Assert.That(enemy.CurrentHp, Is.EqualTo(60));
            Assert.That(other.CurrentHp, Is.EqualTo(60));
            Assert.That(player.CombatStatus.ComboCount, Is.EqualTo(comboBeforeCast));
            Assert.That(skills.EndSkillFrame(final), Is.True);
            Assert.That(skills.TryHitSkill(final, enemy), Is.EqualTo(AttackHit.Ignored));
            Assert.That(player.Commands.TryStartAttack(), Is.True);
        }

        [Test]
        public void AHitCancelsSelectionOrAnAlreadyOpenedHitBatch()
        {
            var player = SkillWaveRewardTests.NewPlayer();
            SkillWaveRewardTests.GrantFirstSkill(player, 1);
            var skills = player.SkillCommands;
            Assert.That(skills.TryOpenSkillSelection(), Is.True);
            ulong first = player.SkillStatus.SelectionId;
            DamagePlayer(player);
            Assert.That(player.SkillStatus.IsSelecting, Is.False);
            Assert.That(skills.TryCastSkill(first, 0, 1d, out _), Is.False);

            SkillCast cast = StartCast(player, 1d);
            var batch = skills.BeginSkillFrame(cast.Id, true, .45f, 1.2d);
            Assert.That(batch.CanHit, Is.True);
            DamagePlayer(player);
            Assert.That(player.IsCastingSkill, Is.False);
            var enemy = new EnemyHealth(100);
            Assert.That(skills.TryHitSkill(batch, enemy), Is.EqualTo(AttackHit.Ignored));
            Assert.That(enemy.CurrentHp, Is.EqualTo(100));
        }

        [Test]
        public void InvalidOrStalledAnimationReleasesTheAction()
        {
            var player = SkillWaveRewardTests.NewPlayer();
            SkillWaveRewardTests.GrantFirstSkill(player, 1);
            var skills = player.SkillCommands;
            SkillCast cast = StartCast(player, 1d);
            Assert.That(skills.BeginSkillFrame(cast.Id, true, float.NaN, 1.1d).IsValid, Is.False);
            Assert.That(player.IsCastingSkill, Is.False);
            cast = StartCast(player, 2d);
            Assert.That(skills.BeginSkillFrame(cast.Id, false, 0f, 4d).IsValid, Is.False);
            Assert.That(player.IsCastingSkill, Is.False);
        }

        internal static SkillCast StartCast(PlayerActions player, double time)
        {
            var skills = player.SkillCommands;
            Assert.That(skills.TryOpenSkillSelection(), Is.True);
            SkillStatus status = player.SkillStatus;
            Assert.That(status.IsUsingBulletTime, Is.True);
            Assert.That(skills.TryCastSkill(status.SelectionId, 0, time, out SkillCast cast), Is.True);
            Assert.That(skills.TryCastSkill(status.SelectionId, 0, time, out _), Is.False);
            return cast;
        }

        internal static void DamagePlayer(PlayerActions player, int damage = 10)
        {
            var enemy = new EnemyActions(new EnemyHealth(100), 1.25f, .75f, damage, .3f, .5f, 1f, 1f, 1f, 1f).Commands;
            enemy.BindTarget(player);
            enemy.Step(true, 1f, 0f, true, .1f);
            Assert.That(enemy.TryHitRicca(enemy.BeginAttackFrame(true, .4f, .1f), player),
                Is.EqualTo(damage >= 100 ? AttackHit.Defeated : AttackHit.Damaged));
        }
    }
}
