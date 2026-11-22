using System;
using IdleRicca.Domain.Gameplay;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>分别持有局内技能目录和四个装备槽，领取时核对已锁定的预告。</summary>
    internal sealed class PlayerSkillSet
    {
        private readonly SkillDefinition[] _catalog;
        private readonly int[] _catalogIndexBySlot = new int[SkillStatus.SlotCount];
        private readonly int[] _levels = new int[SkillStatus.SlotCount];
        private readonly bool _legacyOrder;
        private int _unavailableSkills;

        internal PlayerSkillSet(SkillDefinition[] definitions)
        {
            if (definitions == null)
                throw new ArgumentNullException(nameof(definitions));

            int seen = 0;
            bool legacyOrder = true;
            foreach (SkillDefinition definition in definitions)
            {
                if (!definition.IsValid)
                    throw new ArgumentException("Skill definitions require valid timing and increasing damage.", nameof(definitions));

                int mask = 1 << (int)definition.Id;
                if ((seen & mask) != 0)
                    throw new ArgumentException("Skill identifiers must be unique.", nameof(definitions));
                seen |= mask;
                legacyOrder &= definition.Id <= SkillId.GroundBurst;
            }
            _catalog = (SkillDefinition[])definitions.Clone();
            _legacyOrder = legacyOrder;
        }

        internal bool CanReceiveReward
        {
            get
            {
                int slot = FindRewardSlot();
                return slot >= 0 && (_levels[slot] != 0 || CountUnequipped() > 0);
            }
        }

        internal void SetUnavailableSkills(int mask) => _unavailableSkills = mask;

        internal SkillStatus GetStatus(ulong selectionId, int mana)
        {
            int availableSlots = 0;
            for (int slot = 0; slot < _levels.Length; slot++)
                if (_levels[slot] != 0 && IsAvailable(_catalogIndexBySlot[slot]))
                    availableSlots |= 1 << slot;

            return new SkillStatus(selectionId, mana, availableSlots, GetSlot(0), GetSlot(1), GetSlot(2), GetSlot(3));
        }

        internal bool TryGetEquipped(int slot, out SkillDefinition definition, out int level)
        {
            definition = default;
            level = 0;
            if ((uint)slot >= _levels.Length || _levels[slot] == 0 || !IsAvailable(_catalogIndexBySlot[slot]))
                return false;

            definition = _catalog[_catalogIndexBySlot[slot]];
            level = _levels[slot];
            return true;
        }

        internal bool TryPreview(int cardLevel, Random random, out SkillRewardPreview preview)
        {
            preview = default;
            if (cardLevel < 1 || cardLevel > WaveRewardPool.Levels)
                return false;

            int slot = FindRewardSlot();
            if (slot < 0)
                return false;

            int previous = _levels[slot];
            int index = previous == 0 ? ChooseUnequipped(random) : _catalogIndexBySlot[slot];
            if (index < 0)
                return false;

            preview = new SkillRewardPreview(slot, _catalog[index].Id, previous, Math.Max(previous + 1, cardLevel));
            return true;
        }

        internal bool TryReceive(int cardLevel, SkillRewardPreview preview)
        {
            int slot = FindRewardSlot();
            if (slot < 0 || slot != preview.Slot || cardLevel < 1 || cardLevel > WaveRewardPool.Levels)
                return false;

            int previous = _levels[slot];
            if (preview.PreviousLevel != previous || preview.Level != Math.Max(previous + 1, cardLevel))
                return false;

            if (previous == 0)
            {
                int index = FindCatalogIndex(preview.Skill);
                if (index < 0 || IsEquipped(index) || !IsAvailable(index))
                    return false;

                _catalogIndexBySlot[slot] = index;
            }
            else if (_catalog[_catalogIndexBySlot[slot]].Id != preview.Skill)
            {
                return false;
            }

            _levels[slot] = preview.Level;
            return true;
        }

        internal void Clear() => Array.Clear(_levels, 0, _levels.Length);

        private int FindRewardSlot()
        {
            int slot = -1;
            int lowest = WaveRewardPool.Levels;
            for (int i = 0; i < _levels.Length; i++)
            {
                int level = _levels[i];
                if (level >= lowest || (level > 0 && !IsAvailable(_catalogIndexBySlot[i])))
                    continue;

                slot = i;
                lowest = level;
            }
            return slot;
        }

        private int CountUnequipped()
        {
            int count = 0;
            for (int i = 0; i < _catalog.Length; i++)
                if (IsAvailable(i) && !IsEquipped(i))
                    count++;
            return count;
        }

        private int ChooseUnequipped(Random random)
        {
            int count = CountUnequipped();
            if (count == 0)
                return -1;

            // 为已有占位配置保留其原领取顺序。源技能目录使用单独随机源。
            int choice = _legacyOrder ? 0 : random.Next(count);
            for (int i = 0; i < _catalog.Length; i++)
            {
                if (!IsAvailable(i) || IsEquipped(i))
                    continue;

                if (choice == 0)
                    return i;

                choice--;
            }
            return -1;
        }

        private bool IsAvailable(int catalogIndex) =>
            (_unavailableSkills & (1 << (int)_catalog[catalogIndex].Id)) == 0;

        private bool IsEquipped(int catalogIndex)
        {
            for (int slot = 0; slot < _levels.Length; slot++)
                if (_levels[slot] != 0 && _catalogIndexBySlot[slot] == catalogIndex)
                    return true;
            return false;
        }

        private int FindCatalogIndex(SkillId skill)
        {
            for (int i = 0; i < _catalog.Length; i++)
                if (_catalog[i].Id == skill)
                    return i;
            return -1;
        }

        private (SkillId, int) GetSlot(int slot) =>
            _levels[slot] == 0 ? default : (_catalog[_catalogIndexBySlot[slot]].Id, _levels[slot]);
    }
}
