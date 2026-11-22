using IdleRicca.Application.Gameplay;
using NUnit.Framework;

namespace IdleRicca.Tests
{
    public sealed class SkillManaTests
    {
        [Test]
        public void NewPlayerStartsWithFullMana()
        {
            var player = SkillWaveRewardTests.NewPlayer();
            Assert.That(player.MaxMp, Is.EqualTo(100));
            Assert.That(player.CurrentMp, Is.EqualTo(100));
        }

        [Test]
        public void AValidCastConsumesTenOnceAtTheThreshold()
        {
            var player = SkillWaveRewardTests.NewPlayer(10);
            SkillWaveRewardTests.GrantFirstSkill(player, 1);
            IPlayerSkillCommands skills = player.SkillCommands;
            Assert.That(skills.TryOpenSkillSelection(), Is.True);
            ulong token = player.SkillStatus.SelectionId;
            Assert.That(skills.TryCastSkill(token, 0, 1d, out SkillCast cast), Is.True);
            Assert.That(player.CurrentMp, Is.Zero);
            Assert.That(skills.TryCastSkill(token, 0, 1d, out _), Is.False);
            Assert.That(skills.CancelSkillCast(cast.Id), Is.True);
            Assert.That(player.CurrentMp, Is.Zero);
        }

        [Test]
        public void AnEquippedSkillAtNineManaCannotCastOrSlowTheGame()
        {
            var player = SkillWaveRewardTests.NewPlayer(19);
            SkillWaveRewardTests.GrantFirstSkill(player, 1);
            SkillCast cast = PlayerSkillTests.StartCast(player, 1d);
            Assert.That(player.SkillCommands.CancelSkillCast(cast.Id), Is.True);
            Assert.That(player.CurrentMp, Is.EqualTo(9));
            IPlayerSkillCommands skills = player.SkillCommands;
            Assert.That(skills.TryOpenSkillSelection(), Is.True);
            Assert.That(player.SkillStatus.IsUsingBulletTime, Is.False);
            Assert.That(skills.TryCastSkill(player.SkillStatus.SelectionId, 0, 2d, out _), Is.False);
            Assert.That(player.CurrentMp, Is.EqualTo(9));
        }

        [Test]
        public void RejectedSelectionRetainsManaAndLethalDamageKeepsTheAcceptedCost()
        {
            var player = SkillWaveRewardTests.NewPlayer(20);
            SkillWaveRewardTests.GrantFirstSkill(player, 1);
            IPlayerSkillCommands skills = player.SkillCommands;
            Assert.That(skills.TryOpenSkillSelection(), Is.True);
            ulong expired = player.SkillStatus.SelectionId;
            Assert.That(skills.CancelSkillSelection(expired), Is.True);
            Assert.That(skills.TryCastSkill(expired, 0, 1d, out _), Is.False);
            Assert.That(player.CurrentMp, Is.EqualTo(20));

            SkillCast cast = PlayerSkillTests.StartCast(player, 1d);
            AttackSampleBatch batch = skills.BeginSkillFrame(cast.Id, true, .45f, 1.2d);
            Assert.That(PlayerSkillTests.DamagePlayer(player, 500), Is.EqualTo(AttackHit.Defeated));
            Assert.That(player.CurrentMp, Is.EqualTo(10));
            var enemy = new EnemyHealth(100);
            Assert.That(skills.TryHitSkill(batch, enemy), Is.EqualTo(AttackHit.Ignored));
            Assert.That(enemy.CurrentHp, Is.EqualTo(100));
        }
    }
}
