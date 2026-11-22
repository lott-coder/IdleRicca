using IdleRicca.Domain.Gameplay;

namespace IdleRicca.Application.Gameplay
{
    /// <summary>四槽和选择状态的无分配值快照。</summary>
    public readonly struct SkillStatus
    {
        /// <summary>每局固定槽位数。</summary>
        public const int SlotCount = 4;
        private readonly (SkillId Id, int Level) _first;
        private readonly (SkillId Id, int Level) _second;
        private readonly (SkillId Id, int Level) _third;
        private readonly (SkillId Id, int Level) _fourth;

        internal SkillStatus(ulong selectionId, int currentMp, int availableSlots, (SkillId, int) first, (SkillId, int) second,
            (SkillId, int) third, (SkillId, int) fourth)
        {
            IsEnabled = true;
            SelectionId = selectionId;
            CurrentMp = currentMp;
            AvailableSlots = availableSlots;
            _first = first;
            _second = second;
            _third = third;
            _fourth = fourth;
        }

        /// <summary>指示当前角色是否配置了技能。</summary>
        public bool IsEnabled { get; }
        /// <summary>当前选择编号，零表示关闭。</summary>
        public ulong SelectionId { get; }
        /// <summary>指示技能选择是否打开。</summary>
        public bool IsSelecting => SelectionId != 0;
        /// <summary>有可施放技能时要求选择期间减速。</summary>
        public bool IsUsingBulletTime => IsSelecting && AvailableSlots != 0 && HasMana;
        /// <summary>当前可供技能使用的蓝量。</summary>
        public int CurrentMp { get; }
        /// <summary>蓝量达到统一的施放成本。</summary>
        public bool HasMana => CurrentMp >= PlayerActions.SkillManaCost;
        /// <summary>拥有资源可用的技能槽位位图。</summary>
        public int AvailableSlots { get; }
        /// <summary>是否已有已装备的技能。</summary>
        public bool HasEquippedSkills => _first.Level != 0;
        /// <summary>返回当前槽位是否可选择。</summary>
        public bool CanCastSlot(int slot) =>
            HasMana && (uint)slot < SlotCount && (AvailableSlots & (1 << slot)) != 0;
        /// <summary>查询槽位的技能标识。</summary>
        public SkillId GetSkillId(int slot) => GetSlot(slot).Id;
        /// <summary>查询槽位等级，空槽为零。</summary>
        public int GetLevel(int slot) => GetSlot(slot).Level;

        private (SkillId Id, int Level) GetSlot(int slot) => slot switch
        {
            0 => _first,
            1 => _second,
            2 => _third,
            3 => _fourth,
            _ => default
        };
    }
}
