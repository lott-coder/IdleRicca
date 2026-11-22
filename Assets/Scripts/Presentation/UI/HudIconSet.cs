using System;
using System.Collections.Generic;
using IdleRicca.Domain.Gameplay;
using UnityEngine;
using UnityEngine.Serialization;

namespace IdleRicca.Presentation.UI
{
    /// <summary>提供选技和奖励界面共用的序列化图标。</summary>
    [CreateAssetMenu(menuName = "IdleRicca/UI/HUD Icons")]
    public sealed class HudIconSet : ScriptableObject
    {
        [Serializable]
        private struct SkillIcon
        {
            [FormerlySerializedAs("Skill"), SerializeField] private SkillId _skill;
            [FormerlySerializedAs("Sprite"), SerializeField] private Sprite _sprite;

            public SkillId Skill => _skill;
            public Sprite Sprite => _sprite;
        }

        [SerializeField] private Sprite _emptySlot;
        [SerializeField] private Sprite _health;
        [SerializeField] private Sprite _attack;
        [SerializeField] private Sprite _genericSkill;
        [SerializeField] private SkillIcon[] _skills = Array.Empty<SkillIcon>();

        /// <summary>生命奖励图标。</summary>
        public Sprite Health => _health;
        /// <summary>普攻伤害奖励图标。</summary>
        public Sprite Attack => _attack;
        /// <summary>无法预览具体技能时的奖励图标。</summary>
        public Sprite GenericSkill => _genericSkill;

        /// <summary>按技能 ID 取得图标，支持空槽及通用回退图标。</summary>
        public Sprite GetSkillIcon(SkillId skill)
        {
            if (skill == SkillId.None)
                return _emptySlot;

            foreach (SkillIcon entry in _skills)
            {
                if (entry.Skill == skill)
                    return entry.Sprite ? entry.Sprite : _genericSkill;
            }

            return _genericSkill;
        }

        private void OnValidate()
        {
            var seen = new HashSet<SkillId>();
            foreach (SkillIcon entry in _skills)
            {
                if (entry.Skill == SkillId.None || !Enum.IsDefined(typeof(SkillId), entry.Skill))
                    Debug.LogError($"HUD icon has an invalid skill ID: {entry.Skill}.", this);
                else if (!seen.Add(entry.Skill))
                    Debug.LogError($"HUD icon for {entry.Skill} is configured more than once.", this);
                else if (!entry.Sprite)
                    Debug.LogError($"HUD icon for {entry.Skill} is missing its sprite.", this);
            }
        }
    }
}
