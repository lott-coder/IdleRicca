using System;
using System.Linq;
using IdleRicca.Application.Gameplay;
using IdleRicca.Domain.Gameplay;
using NUnit.Framework;

namespace IdleRicca.Tests
{
    public sealed class WaveRunTests
    {
        [Test]
        public void TenWavesEachOfferExactlyOneRewardGroupAndThenFinish()
        {
            var player = NewPlayer();
            WaveRun run = NewRun(player);
            double now = 0;
            ulong previousOffer = 0;

            for (int wave = 1; wave <= 10; wave++)
            {
                Assert.That(run.Status.Number, Is.EqualTo(wave));
                Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Fighting));
                Assert.That(run.TryChooseReward(previousOffer, 0, now), Is.False);
                ClearSingleEnemyWave(run, player, now++);
                WaveStatus reward = run.Status;
                Assert.That(reward.Number, Is.EqualTo(wave));
                Assert.That(reward.Phase, Is.EqualTo(WaveStatus.RunPhase.Reward));
                Assert.That(reward.OfferId, Is.GreaterThan(previousOffer));
                Assert.That(run.CanSpawn(now), Is.False);
                Assert.That(player.Commands.TryStartAttack(), Is.False);
                Assert.That(player.GetMovement(0, 1, 0, false).IsMoving, Is.False);
                run.Advance();
                Assert.That(run.Status.OfferId, Is.EqualTo(reward.OfferId));
                Assert.That(run.TryChooseReward(reward.OfferId, -1, now), Is.False);
                Assert.That(run.TryChooseReward(previousOffer, 0, now), Is.False);
                Assert.That(run.TryChooseReward(reward.OfferId, 0, now), Is.True);
                Assert.That(run.TryChooseReward(reward.OfferId, 1, now), Is.False);
                previousOffer = reward.OfferId;
            }

            Assert.That(run.Status.Number, Is.EqualTo(10));
            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Won));
            Assert.That(run.CanSpawn(now), Is.False);
            Assert.That(run.TryBeginRestart(), Is.True);
            Assert.That(run.TryBeginRestart(), Is.False);
            run.CancelRestart();
            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Won));
        }

        [Test]
        public void IntervalLimitAndQuotaSurviveFailedOrEarlyRetiredSpawns()
        {
            var player = NewPlayer();
            WaveRun run = NewRun(player, new WaveDefinition(2, 1.5, 3));
            Assert.That(Reserve(run, double.NaN, out _), Is.False);
            Assert.That(run.TryReserveSpawn(EnemyKind.Goblin, 64, true, false, true, 0, out _), Is.False);
            Assert.That(Reserve(run, 0, out ulong failed), Is.True);
            Assert.That(run.Release(failed), Is.True);
            Assert.That(run.Release(failed), Is.False);

            var first = Spawn(run, 0);
            Assert.That(Reserve(run, 1.499, out _), Is.False);
            var retired = Spawn(run, 1.5);
            Assert.That(Reserve(run, 3, out _), Is.False);
            Assert.That(run.Release(retired.Slot), Is.True);

            var second = Spawn(run, 3);
            Assert.That(Swing(player, first.Health), Is.EqualTo(AttackHit.Defeated));
            Assert.That(Swing(player, first.Health), Is.EqualTo(AttackHit.Ignored));
            run.Advance();
            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Fighting));
            Assert.That(Reserve(run, 4.5, out _), Is.False, "A corpse retains its slot.");
            Assert.That(run.Release(first.Slot), Is.True);
            var third = Spawn(run, 4.5);
            Assert.That(Swing(player, second.Health), Is.EqualTo(AttackHit.Defeated));
            Assert.That(run.Release(second.Slot), Is.True);
            Assert.That(Reserve(run, 6, out _), Is.False, "The third live enemy already owns the last quota.");
            run.Advance();
            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Fighting));
            Assert.That(Swing(player, third.Health), Is.EqualTo(AttackHit.Defeated));
            run.Advance();
            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Reward));
        }

        [Test]
        public void LowerNextCapWaitsForCorpsesAndLaterWavesAllowSixSlots()
        {
            var player = NewPlayer();
            WaveDefinition[] definitions = Definitions(new WaveDefinition(6, .25, 6));
            definitions[1] = new WaveDefinition(1, .25, 1);
            WaveRun run = NewRun(player, definitions);
            var firstWave = new (ulong Slot, EnemyHealth Health)[6];
            for (int i = 0; i < firstWave.Length; i++)
                firstWave[i] = Spawn(run, i * .25);
            Assert.That(run.OccupiedSlots, Is.EqualTo(6));
            foreach (var enemy in firstWave)
                Assert.That(Swing(player, enemy.Health), Is.EqualTo(AttackHit.Defeated));

            run.Advance();
            Assert.That(run.TryChooseReward(run.Status.OfferId, 0, 2), Is.True);
            Assert.That(run.Status.Number, Is.EqualTo(2));
            for (int i = 0; i < firstWave.Length; i++)
            {
                Assert.That(run.CanSpawn(2), Is.False);
                Assert.That(run.Release(firstWave[i].Slot), Is.True);
            }
            ClearSingleEnemyWave(run, player, 2);
            Assert.That(run.TryChooseReward(run.Status.OfferId, 0, 3), Is.True);
            for (int i = 0; i < 6; i++)
                Spawn(run, 3 + i * .25);
            Assert.That(run.OccupiedSlots, Is.EqualTo(6));
        }

        [Test]
        public void AHealthObjectCanOnlyBeBoundToOneSlot()
        {
            WaveRun run = NewRun(NewPlayer(), new WaveDefinition(2, 1, 3));
            var first = Spawn(run, 0);
            Assert.That(Reserve(run, 1, out ulong other), Is.True);
            Assert.That(run.ConfirmSpawn(other, first.Health, 1), Is.False);
            Assert.That(run.Release(other), Is.True);
            Assert.That(run.ConfirmSpawn(first.Slot, first.Health, 1), Is.False);
        }

        [Test]
        public void WeightedPoolsHaveDistinctOptionsAndPermitRepeatedTypes()
        {
            var pool = Pool(16);
            var tally = new int[3];
            bool sawTwoAttacksAndHealth = false;
            for (int i = 0; i < 5000; i++)
            {
                WaveReward[] group = new WaveReward[3];
                pool.Draw(group);
                Assert.That(group.Select(reward => (reward.Kind, reward.Level)).Distinct().Count(), Is.EqualTo(3));
                foreach (var reward in group)
                    tally[(int)reward.Kind]++;
                sawTwoAttacksAndHealth |= group.Count(reward => reward.Kind == WaveReward.RewardKind.Attack) == 2
                    && group.Any(reward => reward.Kind == WaveReward.RewardKind.Health);
            }

            Assert.That(tally[(int)WaveReward.RewardKind.Skill], Is.LessThan(tally[(int)WaveReward.RewardKind.Health]));
            Assert.That(tally[(int)WaveReward.RewardKind.Skill], Is.LessThan(tally[(int)WaveReward.RewardKind.Attack]));
            Assert.That(sawTwoAttacksAndHealth, Is.True);
        }

        [TestCase(WaveReward.RewardKind.Health)]
        [TestCase(WaveReward.RewardKind.Attack)]
        [TestCase(WaveReward.RewardKind.Skill)]
        public void ChosenRewardUsesTheSelectionDataExactlyOnce(WaveReward.RewardKind kind)
        {
            var player = NewPlayer();
            WaveRun run = NewRun(player);
            WaveReward appliedSkill = default;
            int skillCount = 0;
            run.SkillRewardSelected += reward =>
            {
                appliedSkill = reward;
                skillCount++;
            };
            Assert.That(AttackPlayer(player, 60), Is.EqualTo(AttackHit.Damaged));

            int healthBonus = 0;
            int attackBonus = 0;
            int matches = 0;
            int expectedSkillCount = 0;
            for (int wave = 1; wave <= 10; wave++)
            {
                ClearSingleEnemyWave(run, player, wave);
                WaveStatus status = run.Status;
                int index = Enumerable.Range(0, 3).FirstOrDefault(i => status.GetReward(i).Kind == kind);
                WaveReward selected = status.GetReward(index);
                if (selected.Kind == kind)
                    matches++;
                Assert.That(run.TryChooseReward(status.OfferId, index, wave), Is.True);
                Assert.That(run.TryChooseReward(status.OfferId, index, wave), Is.False);

                if (selected.Kind == WaveReward.RewardKind.Health)
                    healthBonus += selected.Amount;
                else if (selected.Kind == WaveReward.RewardKind.Attack)
                    attackBonus += selected.Amount;
                else
                {
                    expectedSkillCount++;
                    Assert.That(appliedSkill.Level, Is.EqualTo(selected.Level));
                }

                Assert.That(player.CurrentHp, Is.EqualTo(40 + healthBonus));
                Assert.That(player.MaxHp, Is.EqualTo(100 + healthBonus));
                Assert.That(player.AttackDamage, Is.EqualTo(25 + attackBonus));
                if (wave < 10)
                {
                    var untrackedEnemy = new EnemyHealth(1000);
                    Assert.That(Swing(player, untrackedEnemy), Is.EqualTo(AttackHit.Damaged));
                    Assert.That(untrackedEnemy.CurrentHp, Is.EqualTo(1000 - 25 - attackBonus));
                }
            }
            Assert.That(matches, Is.GreaterThan(0));
            Assert.That(skillCount, Is.EqualTo(expectedSkillCount));
        }

        [Test]
        public void DeathWinsOverTheLastDefeatAndRestoresBaselineBonuses()
        {
            var player = NewPlayer();
            WaveRun run = NewRun(player);
            ClearSingleEnemyWave(run, player, 0);
            Assert.That(run.TryChooseReward(run.Status.OfferId, 0, 1), Is.True);
            var enemy = Spawn(run, 1);
            Assert.That(Swing(player, enemy.Health), Is.EqualTo(AttackHit.Defeated));
            Assert.That(AttackPlayer(player, 1000), Is.EqualTo(AttackHit.Defeated));
            run.Advance();

            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Defeated));
            Assert.That(run.Status.Number, Is.EqualTo(1));
            Assert.That(run.Status.OfferId, Is.Zero);
            Assert.That(run.CanSpawn(20), Is.False);
            Assert.That(player.CurrentHp, Is.Zero);
            Assert.That(player.MaxHp, Is.EqualTo(100));
            Assert.That(player.AttackDamage, Is.EqualTo(25));
            Assert.That(run.TryBeginRestart(), Is.True);
            Assert.That(run.TryBeginRestart(), Is.False);
        }

        [Test]
        public void InvalidConfigurationCannotStartAConfiguredRun()
        {
            Assert.That(new WaveDefinition(1, double.PositiveInfinity, 1).IsValid, Is.False);
            Assert.That(new WaveDefinition(0, 1, 1).IsValid, Is.False);
            Assert.That(new WaveDefinition(1, 1, 0).IsValid, Is.False);
            Assert.Throws<ArgumentException>(() => NewRun(NewPlayer(), new[] { new WaveDefinition(3, 1, 3) }));
            Assert.DoesNotThrow(() => new WaveRewardPool(45, 45, 45, new[] { 10, 20, 30 }, new[] { 4, 8, 12 }));
            Assert.Throws<ArgumentException>(() => new WaveRewardPool(45, 45, 0, new[] { 10, 20, 30 }, new[] { 4, 8, 12 }));
            Assert.Throws<ArgumentException>(() => new WaveRewardPool(45, 45, 10, new[] { 10, 10, 30 }, new[] { 4, 8, 12 }));
        }

        private static WaveRewardPool Pool(int seed) => new WaveRewardPool(45, 45, 10, new[] { 10, 20, 30 }, new[] { 4, 8, 12 }, seed);
        private static PlayerActions NewPlayer() => new PlayerActions(25, .2f, .5f);
        private static WaveDefinition[] Definitions(WaveDefinition wave) => Enumerable.Repeat(wave, 10).ToArray();
        private static WaveRun NewRun(PlayerActions player) => NewRun(player, new WaveDefinition(1, .25, 1));
        private static WaveRun NewRun(PlayerActions player, WaveDefinition wave) => NewRun(player, Definitions(wave));
        private static WaveRun NewRun(PlayerActions player, WaveDefinition[] definitions) => new WaveRun(player, definitions, Pool(16), 7, 14, 137);
        private static bool Reserve(WaveRun run, double now, out ulong slot) =>
            run.TryReserveSpawn(EnemyKind.Goblin, 64, true, true, true, now, out slot);

        private static (ulong Slot, EnemyHealth Health) Spawn(WaveRun run, double now)
        {
            Assert.That(Reserve(run, now, out ulong slot), Is.True);
            var health = new EnemyHealth(1);
            Assert.That(run.ConfirmSpawn(slot, health, now), Is.True);
            return (slot, health);
        }

        private static void ClearSingleEnemyWave(WaveRun run, PlayerActions player, double now)
        {
            var enemy = Spawn(run, now);
            Assert.That(Swing(player, enemy.Health), Is.EqualTo(AttackHit.Defeated));
            Assert.That(run.Release(enemy.Slot), Is.True);
            run.Advance();
        }

        private static AttackHit Swing(PlayerActions player, EnemyHealth enemy)
        {
            var commands = player.Commands;
            Assert.That(commands.TryStartAttack(), Is.True);
            var batch = commands.BeginAttackFrame(true, 1f, .1f);
            var result = commands.TryHit(batch, enemy);
            commands.EndAttackFrame(batch);
            return result;
        }

        private static AttackHit AttackPlayer(PlayerActions player, int damage)
        {
            var command = new EnemyActions(new EnemyHealth(100), 1.25f, .75f, damage, .3f, .5f, 1f, 1f, 1f, 1f).Commands;
            command.BindTarget(player);
            command.Step(true, 1f, 0f, true, .1f);
            return command.TryHitRicca(command.BeginAttackFrame(true, .4f, .1f), player);
        }
    }
}
