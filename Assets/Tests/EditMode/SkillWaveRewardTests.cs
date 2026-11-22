using System;
using System.Collections.Generic;
using IdleRicca.Application.Gameplay;
using IdleRicca.Domain.Gameplay;
using NUnit.Framework;

namespace IdleRicca.Tests
{
    public sealed class SkillWaveRewardTests
    {
        [Test]
        public void FourGrantsFillDistinctSlotsThenUpgradeTheLeftmostLowestRank()
        {
            var player = NewPlayer();
            int[] levels = { 1, 3, 1, 2, 3, 3, 3 };
            var run = NewRun(player, FindSeed(levels));
            int notices = 0;
            run.SkillRewardSelected += _ => notices++;
            for (int i = 0; i < levels.Length; i++)
            {
                ClearWave(run, player, 3d * i);
                WaveStatus offer = run.Status;
                int index = FindSkill(offer, levels[i]);
                Assert.That(index, Is.GreaterThanOrEqualTo(0));
                Assert.That(run.TryGetSkillRewardPreview(offer.OfferId, index, out var preview), Is.True);
                Assert.That(run.TryChooseReward(offer.OfferId, index, 3d * i), Is.True);
                Assert.That(run.TryChooseReward(offer.OfferId, index, 3d * i), Is.False);
                Assert.That(player.SkillStatus.GetSkillId(preview.Slot), Is.EqualTo(preview.Skill));
                Assert.That(player.SkillStatus.GetLevel(preview.Slot), Is.EqualTo(preview.Level));
                if (i == 0 || i == 4)
                {
                    SkillCast cast = PlayerSkillTests.StartCast(player, 3d * i + 0.1d);
                    var enemy = new EnemyHealth(100);
                    var window = player.SkillCommands.BeginSkillFrame(cast.Id, true, 1f, 3d * i + 0.8d);
                    Assert.That(player.SkillCommands.TryHitSkill(window, enemy), Is.EqualTo(AttackHit.Damaged));
                    Assert.That(player.SkillCommands.EndSkillFrame(window), Is.True);
                    Assert.That(enemy.CurrentHp, Is.EqualTo(i == 0 ? 70 : 45));
                }
                if (i == 4)
                {
                    Assert.That(preview.Slot, Is.Zero);
                    Assert.That(preview.PreviousLevel, Is.EqualTo(1));
                }
            }

            Assert.That(notices, Is.EqualTo(7));
            for (int i = 0; i < SkillStatus.SlotCount; i++)
            {
                Assert.That(player.SkillStatus.GetSkillId(i), Is.EqualTo((SkillId)(i + 1)));
                Assert.That(player.SkillStatus.GetLevel(i), Is.EqualTo(3));
            }

            ClearWave(run, player, 30d);
            for (int i = 0; i < WaveStatus.OfferSize; i++)
            {
                var reward = run.Status.GetReward(i);
                Assert.That(reward.Kind, Is.Not.EqualTo(WaveReward.RewardKind.Skill));
                for (int j = 0; j < i; j++)
                {
                    var previous = run.Status.GetReward(j);
                    Assert.That((previous.Kind, previous.Level), Is.Not.EqualTo((reward.Kind, reward.Level)));
                }
            }
        }

        [Test]
        public void LegacyDrawAndEventRemainAvailableWithoutSkillConfiguration()
        {
            const int seed = 17;
            var player = new PlayerActions(25, .3f, .6f);
            var run = NewRun(player, seed);
            var pool = Pool(seed);
            var expected = new WaveReward[WaveStatus.OfferSize];
            int notifications = 0;
            int selectedSkills = 0;
            run.SkillRewardSelected += _ => notifications++;
            for (int wave = 0; wave < 10; wave++)
            {
                pool.Draw(expected);
                ClearWave(run, player, wave);
                WaveStatus status = run.Status;
                for (int i = 0; i < expected.Length; i++)
                {
                    WaveReward reward = status.GetReward(i);
                    Assert.That((reward.Kind, reward.Level, reward.Amount),
                        Is.EqualTo((expected[i].Kind, expected[i].Level, expected[i].Amount)));
                }
                int selected = FindSkill(status);
                if (selected >= 0)
                    selectedSkills++;
                Assert.That(run.TryChooseReward(status.OfferId, Math.Max(0, selected), wave), Is.True);
            }
            Assert.That(notifications, Is.EqualTo(selectedSkills));
            Assert.That(notifications, Is.Positive);
        }

        [Test]
        public void FinalWaveCommitsSkillAndNotifiesExactlyOnce()
        {
            var player = NewPlayer();
            var requestedLevels = new int[10];
            requestedLevels[9] = 3;
            var run = NewRun(player, FindSeed(requestedLevels));
            WaveStatus.RunPhase observedPhase = default;
            int notices = 0;
            run.SkillRewardSelected += _ => { observedPhase = run.Status.Phase; notices++; };

            for (int wave = 0; wave < 9; wave++)
            {
                ClearWave(run, player, wave);
                var offer = run.Status;
                int stat = 0;
                while (offer.GetReward(stat).Kind == WaveReward.RewardKind.Skill)
                    stat++;
                Assert.That(run.TryChooseReward(offer.OfferId, stat, wave), Is.True);
            }

            ClearWave(run, player, 9);
            WaveStatus final = run.Status;
            int index = FindSkill(final, 3);
            Assert.That(run.TryChooseReward(final.OfferId, index, 9), Is.True);
            Assert.That(run.TryChooseReward(final.OfferId, index, 9), Is.False);
            Assert.That(player.SkillStatus.GetLevel(0), Is.EqualTo(3));
            Assert.That(notices, Is.EqualTo(1));
            Assert.That(observedPhase, Is.EqualTo(WaveStatus.RunPhase.Won));
            Assert.That(player.SkillCommands.TryOpenSkillSelection(), Is.False);
        }

        [Test]
        public void WaveTransitionRejectsDirectSkillCommandsAndInvalidatesBatches()
        {
            var player = NewPlayer();
            var run = NewRun(player, FindSeed(new[] { 1 }));
            ClearWave(run, player, 0);
            var first = run.Status;
            Assert.That(run.TryChooseReward(first.OfferId, FindSkill(first, 1), 0), Is.True);

            var cast = PlayerSkillTests.StartCast(player, 1d);
            Assert.That(run.TryReserveSpawn(EnemyKind.Goblin, 64, true, true, true, 1d, out ulong slot), Is.True);
            var lastEnemy = new EnemyHealth(1);
            Assert.That(run.ConfirmSpawn(slot, lastEnemy, 1d), Is.True);
            var batch = player.SkillCommands.BeginSkillFrame(cast.Id, true, .5f, 1.2d);
            Assert.That(player.SkillCommands.TryHitSkill(batch, lastEnemy), Is.EqualTo(AttackHit.Defeated));
            run.Advance();

            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Reward));
            Assert.That(player.SkillCommands.TryOpenSkillSelection(), Is.False);
            Assert.That(player.SkillCommands.TryCastSkill(1, 0, 1.3d, out _), Is.False);
            Assert.That(player.SkillCommands.TryHitSkill(batch, new EnemyHealth(100)), Is.EqualTo(AttackHit.Ignored));
            Assert.That(player.IsCastingSkill, Is.False);
        }

        [Test]
        public void AllFiveCatalogSkillsCanBeAdvertisedAndGrantedInFourSlots()
        {
            var encountered = new HashSet<SkillId>();
            int rewardSeed = FindSeed(new[] { 1 });
            for (int skillSeed = 0; skillSeed < 128; skillSeed++)
            {
                var definitions = new SkillDefinition[5];
                for (int n = 0; n < definitions.Length; n++)
                    definitions[n] = new SkillDefinition((SkillId)((int)SkillId.SwordThrow + n), 25, 35, 45, .2f, .6f, 1f);
                var player = new PlayerActions(25, .3f, .6f, definitions, 8, 100, 100);
                var run = NewRun(player, rewardSeed, skillSeed);
                ClearWave(run, player, 0);
                WaveStatus offer = run.Status;
                int index = FindSkill(offer, 1);
                Assert.That(run.TryGetSkillRewardPreview(offer.OfferId, index, out var preview), Is.True);
                Assert.That(run.TryGetSkillRewardPreview(offer.OfferId, index, out var same), Is.True);
                Assert.That(same.Skill, Is.EqualTo(preview.Skill));
                Assert.That(run.TryChooseReward(offer.OfferId, index, 0), Is.True);
                Assert.That(player.SkillStatus.GetSkillId(0), Is.EqualTo(preview.Skill));
                Assert.That(player.SkillStatus.GetSkillId(SkillStatus.SlotCount), Is.EqualTo(SkillId.None));
                encountered.Add(preview.Skill);
            }
            Assert.That(encountered, Is.EquivalentTo(new[] {
                SkillId.SwordThrow, SkillId.LightningRush, SkillId.BlazeCharge, SkillId.FrozenOrb, SkillId.HolyGeyser
            }));
        }

        [Test]
        public void InsufficientManaFiltersSkillsAndKeepsThreeUniqueAttributeCards()
        {
            var player = NewPlayer(9);
            int seed = FindSeed(new[] { 1 });
            WaveRun run = NewRun(player, seed);
            ClearWave(run, player, 0);
            var distinct = new HashSet<(WaveReward.RewardKind, int)>();
            for (int i = 0; i < WaveStatus.OfferSize; i++)
            {
                var choice = run.Status.GetReward(i);
                Assert.That(choice.Kind, Is.Not.EqualTo(WaveReward.RewardKind.Skill));
                Assert.That(distinct.Add((choice.Kind, choice.Level)), Is.True);
            }
        }

        [Test]
        public void EqualWeightsGiveEachTypeTheSameFirstCardRange()
        {
            var offer = new WaveReward[WaveStatus.OfferSize];
            for (int seed = 0; seed < 24; seed++)
            {
                var pool = new WaveRewardPool(45, 45, 45, new[] { 10, 20, 30 }, new[] { 4, 8, 12 }, seed);
                pool.Draw(offer);
                int roll = new Random(seed).Next(405);
                Assert.That((int)offer[0].Kind, Is.EqualTo(roll / 135));
            }
        }

        [Test]
        public void UnavailableEquippedSkillStaysVisibleAndCannotCastOrBeOffered()
        {
            var player = new PlayerActions(25, .3f, .6f, new[]
            {
                new SkillDefinition(SkillId.SwordThrow, 34, 46, 60, .35f, .92f, .9167f)
            });
            var run = NewRun(player, FindSeed(new[] { 1 }));
            ClearWave(run, player, 0);
            var offer = run.Status;
            Assert.That(run.TryChooseReward(offer.OfferId, FindSkill(offer, 1), 0), Is.True);

            var skills = player.SkillCommands;
            skills.SetUnavailableSkills(1 << (int)SkillId.SwordThrow);
            Assert.That(skills.TryOpenSkillSelection(), Is.True);
            var status = player.SkillStatus;
            Assert.That(status.GetSkillId(0), Is.EqualTo(SkillId.SwordThrow));
            Assert.That(status.IsUsingBulletTime, Is.False);
            Assert.That(status.CanCastSlot(0), Is.False);
            Assert.That(skills.TryCastSkill(status.SelectionId, 0, 1, out _), Is.False);
            Assert.That(player.CurrentMp, Is.EqualTo(100));
            Assert.That(skills.CancelSkillSelection(status.SelectionId), Is.True);

            ClearWave(run, player, 1);
            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Reward));
            Assert.That(FindSkill(run.Status), Is.EqualTo(-1));
        }

        [Test]
        public void ASourceSkillDefeatAdvancesTheWaveExactlyOnce()
        {
            var player = new PlayerActions(25, .3f, .6f, new[]
            {
                new SkillDefinition(SkillId.SwordThrow, 34, 46, 60, .35f, .92f, .9167f)
            });
            var run = NewRun(player, FindSeed(new[] { 1 }));
            ClearWave(run, player, 0);
            var offer = run.Status;
            Assert.That(run.TryChooseReward(offer.OfferId, FindSkill(offer, 1), 0), Is.True);
            Assert.That(run.TryReserveSpawn(EnemyKind.Goblin, 64, true, true, true, 1, out ulong slot), Is.True);
            var target = new EnemyHealth(34);
            Assert.That(run.ConfirmSpawn(slot, target, 1), Is.True);

            var skills = player.SkillCommands;
            var cast = PlayerSkillTests.StartCast(player, 1);
            var batch = skills.BeginSkillFrame(cast.Id, true, 1f, 1.5);
            Assert.That(skills.TryHitSkill(batch, target), Is.EqualTo(AttackHit.Defeated));
            Assert.That(skills.TryHitSkill(batch, target), Is.EqualTo(AttackHit.Ignored));
            Assert.That(skills.EndSkillFrame(batch), Is.True);
            run.Advance();
            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Reward));
            Assert.That(run.Status.Number, Is.EqualTo(2));
            ulong secondOffer = run.Status.OfferId;
            run.Advance();
            Assert.That(run.Status.OfferId, Is.EqualTo(secondOffer));
        }

        [Test]
        public void BlazeChargeStillGrantsTheBuffWhenItsHitClearsTheWave()
        {
            var player = new PlayerActions(25, .3f, .6f, new[]
            {
                new SkillDefinition(SkillId.BlazeCharge, 38, 52, 68, .26f, .69f, 1f)
            });
            var run = NewRun(player, FindSeed(new[] { 1 }));
            ClearWave(run, player, 0d);
            WaveStatus offer = run.Status;
            Assert.That(run.TryChooseReward(offer.OfferId, FindSkill(offer, 1), 0d), Is.True);
            Assert.That(run.TryReserveSpawn(EnemyKind.Goblin, 64, true, true, true, 1d, out ulong slot), Is.True);
            var target = new EnemyHealth(38);
            Assert.That(run.ConfirmSpawn(slot, target, 1d), Is.True);

            SkillCast cast = PlayerSkillTests.StartCast(player, 1d);
            var skill = player.SkillCommands;
            var batch = skill.BeginSkillFrame(cast.Id, true, .45f, 1.5d);
            Assert.That(skill.TryHitSkill(batch, target), Is.EqualTo(AttackHit.Defeated));
            Assert.That(skill.EndSkillFrame(batch), Is.False);
            run.Advance();
            Assert.That(run.Status.Phase, Is.EqualTo(WaveStatus.RunPhase.Reward));
            Assert.That(player.IsCastingSkill, Is.False);
            Assert.That(player.AttackDamage, Is.EqualTo(30));
            player.Commands.AdvanceCombatTime(21.5d);
            Assert.That(player.AttackDamage, Is.EqualTo(25));
        }

        internal static PlayerActions NewPlayer(int startingMp = 100) => new PlayerActions(25, .3f, .6f, new[]
        {
            new SkillDefinition(SkillId.QuickCut, 30, 40, 55, .3f, .5f, .6f),
            new SkillDefinition(SkillId.Thrust, 35, 50, 65, .4f, .6f, .7f),
            new SkillDefinition(SkillId.Sweep, 30, 45, 60, .4f, .6f, .8f),
            new SkillDefinition(SkillId.GroundBurst, 35, 50, 65, .5f, .7f, 1f)
        }, 8, 100, startingMp);

        internal static void GrantFirstSkill(PlayerActions player, int level)
        {
            var run = NewRun(player, FindSeed(new[] { level }));
            ClearWave(run, player, 0);
            var offer = run.Status;
            Assert.That(run.TryChooseReward(offer.OfferId, FindSkill(offer, level), 0), Is.True);
        }

        private static WaveRun NewRun(PlayerActions player, int seed, int skillSeed = 137)
        {
            var waves = new WaveDefinition[WaveStatus.TotalWaves];
            for (int i = 0; i < waves.Length; i++)
                waves[i] = new WaveDefinition(1, .25, 1);
            return new WaveRun(player, waves, Pool(seed), 7, 14, skillSeed);
        }

        private static WaveRewardPool Pool(int seed) =>
            new WaveRewardPool(45, 45, 44, new[] { 10, 20, 30 }, new[] { 4, 8, 12 }, seed);

        private static void ClearWave(WaveRun run, PlayerActions player, double now)
        {
            Assert.That(run.TryReserveSpawn(EnemyKind.Goblin, 64, true, true, true, now, out ulong slot), Is.True);
            var health = new EnemyHealth(1);
            Assert.That(run.ConfirmSpawn(slot, health, now), Is.True);
            var commands = player.Commands;
            Assert.That(commands.TryStartAttack(), Is.True);
            var batch = commands.BeginAttackFrame(true, 1f, .1f);
            Assert.That(commands.TryHit(batch, health), Is.EqualTo(AttackHit.Defeated));
            commands.EndAttackFrame(batch);
            Assert.That(run.Release(slot), Is.True);
            run.Advance();
        }

        private static int FindSkill(WaveStatus offer, int level = 0)
        {
            for (int i = 0; i < WaveStatus.OfferSize; i++)
            {
                WaveReward reward = offer.GetReward(i);
                if (reward.Kind == WaveReward.RewardKind.Skill && (level == 0 || reward.Level == level))
                    return i;
            }
            return -1;
        }

        private static int FindSeed(int[] levels)
        {
            var draw = new WaveReward[WaveStatus.OfferSize];
            for (int seed = 0; seed < 200000; seed++)
            {
                var pool = Pool(seed);
                bool matches = true;
                foreach (int level in levels)
                {
                    pool.Draw(draw);
                    if (level != 0 && !Array.Exists(draw, reward => reward.Kind == WaveReward.RewardKind.Skill && reward.Level == level))
                    {
                        matches = false;
                        break;
                    }
                    if (level == 0 && Array.TrueForAll(draw, reward => reward.Kind == WaveReward.RewardKind.Skill))
                    {
                        matches = false;
                        break;
                    }
                }
                if (matches)
                    return seed;
            }
            throw new InvalidOperationException("No deterministic reward sequence found.");
        }
    }
}
