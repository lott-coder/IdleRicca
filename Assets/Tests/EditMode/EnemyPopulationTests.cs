using System;
using IdleRicca.Application.Gameplay;
using NUnit.Framework;

namespace IdleRicca.Tests
{
    public sealed class EnemyPopulationTests
    {
        [TestCase(1)]
        [TestCase(3)]
        public void SlotsIncludeRetiringInstancesAndOnlyReleaseOnce(int limit)
        {
            var population = new EnemyPopulation(limit, 7f, 14f, 41);
            IEnemySpawnCommands commands = population.Commands;
            var slots = new ulong[limit];
            for (int index = 0; index < limit; index++)
            {
                Assert.That(commands.TryReserve(EnemyKind.Goblin, 49f, true, true, true, out slots[index]), Is.True);
                Assert.That(commands.ConfirmSpawn(slots[index]), Is.True);
            }

            Assert.That(population.Count, Is.EqualTo(limit));
            Assert.That(commands.TryReserve(EnemyKind.Skeleton, 100f, true, true, true, out _), Is.False);
            Assert.That(commands.Release(slots[0]), Is.True);
            Assert.That(commands.Release(slots[0]), Is.False);
            Assert.That(commands.TryReserve(EnemyKind.Hellhound, 196f, true, true, true, out var replacement), Is.True);
            Assert.That(replacement, Is.Not.EqualTo(slots[0]));
            Assert.That(population.Count, Is.EqualTo(limit));
        }

        [Test]
        public void LargeLimitDoesNotAllocateForUnusedSlots()
        {
            var population = new EnemyPopulation(int.MaxValue, 7f, 14f, 41);
            Assert.That(population.Commands.TryReserve(EnemyKind.Hellhound, 100f, true, true, true, out var slot), Is.True);
            Assert.That(population.Commands.Release(slot), Is.True);
        }

        [Test]
        public void UnsafeOrOutOfRangeCandidateDoesNotReserve()
        {
            var population = new EnemyPopulation(3, 7f, 14f, 41);
            var commands = population.Commands;
            Assert.That(commands.TryReserve(EnemyKind.Goblin, 100f, false, true, true, out _), Is.False);
            Assert.That(commands.TryReserve(EnemyKind.Goblin, 100f, true, false, true, out _), Is.False);
            Assert.That(commands.TryReserve(EnemyKind.Goblin, 100f, true, true, false, out _), Is.False);
            Assert.That(commands.TryReserve(EnemyKind.Goblin, 48.99f, true, true, true, out _), Is.False);
            Assert.That(commands.TryReserve(EnemyKind.Goblin, 196.01f, true, true, true, out _), Is.False);
            Assert.That(commands.TryReserve(EnemyKind.Goblin, float.NaN, true, true, true, out _), Is.False);
            Assert.That(commands.TryReserve((EnemyKind)99, 100f, true, true, true, out _), Is.False);
            Assert.That(population.Count, Is.Zero);
        }

        [Test]
        public void SeededCandidatesAreReproducibleAndCoverTheSearchRadius()
        {
            var first = new EnemyPopulation(3, 7f, 14f, 114);
            var repeat = new EnemyPopulation(3, 7f, 14f, 114);
            var other = new EnemyPopulation(3, 7f, 14f, 115);
            var expected = first.Commands.SampleCandidate();
            var duplicate = repeat.Commands.SampleCandidate();
            Assert.That(duplicate.X, Is.EqualTo(expected.X));
            Assert.That(duplicate.Z, Is.EqualTo(expected.Z));
            Assert.That(other.Commands.SampleCandidate().X, Is.Not.EqualTo(expected.X));

            bool sampledInsideMinimumPathDistance = false;
            for (int i = 0; i < 40; i++)
            {
                var candidate = first.Commands.SampleCandidate();
                double radius = Math.Sqrt(candidate.X * candidate.X + candidate.Z * candidate.Z);
                Assert.That(radius, Is.LessThanOrEqualTo(14));
                sampledInsideMinimumPathDistance |= radius < 7;
            }

            Assert.That(sampledInsideMinimumPathDistance, Is.True,
                "A short straight-line distance can still have a legal navigation distance.");
        }

        [Test]
        public void OpeningRosterPrefersAllKindsAndFailedReservationDoesNotConfirm()
        {
            var population = new EnemyPopulation(3, 7f, 14f, 41);
            var commands = population.Commands;
            Assert.That(commands.TryReserve(EnemyKind.Goblin, 49f, true, true, true, out var failed), Is.True);
            Assert.That(commands.Release(failed), Is.True);
            var initial = new[]
            {
                commands.SampleCandidate().Kind,
                commands.SampleCandidate().Kind,
                commands.SampleCandidate().Kind,
            };
            Assert.That(initial, Is.EquivalentTo(new[] { EnemyKind.Goblin, EnemyKind.Skeleton, EnemyKind.Hellhound }));
        }
    }
}
