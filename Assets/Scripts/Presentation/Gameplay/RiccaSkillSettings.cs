using System;
using System.Collections.Generic;
using IdleRicca.Domain.Gameplay;
using UnityEngine;

namespace IdleRicca.Presentation.Gameplay
{
    /// <summary>序列化技能目录、原动作和伤害窗口。</summary>
    [CreateAssetMenu(menuName = "IdleRicca/Ricca Skills")]
    public sealed class RiccaSkillSettings : ScriptableObject
    {
        [SerializeField, Range(.01f, 1f)] private float _slowMultiplier = .2f;
        [SerializeField] private SkillView[] _skills;

        /// <summary>选择可用技能时相对于此前战斗速度的倍率。</summary>
        public float SlowMultiplier => _slowMultiplier;

        internal bool TryBuild(Animator animator, out SkillDefinition[] definitions,
            out Dictionary<AnimationClip, int> stateHashes)
        {
            definitions = null;
            stateHashes = null;
            if (!animator || !animator.runtimeAnimatorController || !(_slowMultiplier > 0f && _slowMultiplier <= 1f) ||
                _skills == null || _skills.Length > (int)SkillId.HolyGeyser)
                return false;

            var configured = new List<SkillDefinition>(_skills.Length);
            var hashes = new Dictionary<AnimationClip, int>();
            var clips = animator.runtimeAnimatorController.animationClips;
            int seen = 0;
            foreach (SkillView view in _skills)
            {
                if (view == null)
                {
                    Debug.LogError("Ricca skill catalog contains an empty definition.", this);
                    continue;
                }

                if (view.Id < SkillId.QuickCut || view.Id > SkillId.HolyGeyser)
                {
                    Debug.LogError($"Ricca skill catalog contains an invalid ID: {view.Id}.", this);
                    continue;
                }

                int mask = 1 << (int)view.Id;
                if ((seen & mask) != 0)
                {
                    Debug.LogError($"Ricca skill {view.Id} is declared more than once.", this);
                    continue;
                }
                seen |= mask;

                string issue = FindBindingIssue(view, animator, clips);
                if (issue != null)
                {
                    Debug.LogError($"Ricca skill {view.Id} is unavailable: {issue}.", this);
                    continue;
                }

                configured.Add(view.Definition);
                hashes.TryAdd(view.Animation, StateHash(view.Animation));
                foreach (AnimationClip variant in view.Variants)
                    hashes.TryAdd(variant, StateHash(variant));
            }

            definitions = configured.ToArray();
            stateHashes = hashes;
            return true;
        }

        private static string FindBindingIssue(SkillView view, Animator animator, AnimationClip[] controllerClips)
        {
            if (view.RequiresEffect && !view.EffectRoot)
                return "required effect is missing";
            if (view.RequiresEffect && !view.EffectRoot.GetComponentInChildren<Renderer>(true))
                return "required effect has no renderer";
            if (!view.Animation)
                return "main animation is missing";
            if (!view.IsValid)
                return "invalid damage, size, motion, variants or timing configuration";
            if (!HasBinding(view.Animation, animator, controllerClips))
                return "missing Animator binding for " + view.Animation.name;
            for (int i = 0; i < view.Variants.Length; i++)
            {
                AnimationClip clip = view.Variants[i];
                if (!HasBinding(clip, animator, controllerClips))
                    return clip ? "missing Animator binding for " + clip.name : "animation variant " + i + " is missing";
            }

            return null;
        }

        private static bool HasBinding(AnimationClip clip, Animator animator, AnimationClip[] controllerClips) =>
            clip && !clip.isLooping && clip.length > 0f && Array.IndexOf(controllerClips, clip) >= 0 &&
            animator.HasState(0, StateHash(clip));

        private static int StateHash(AnimationClip clip) => Animator.StringToHash("Base Layer." + clip.name);

        internal SkillView GetView(SkillId id)
        {
            foreach (SkillView view in _skills)
                if (view != null && view.Id == id)
                    return view;
            return null;
        }

        /// <summary>返回技能名称。</summary>
        public static string GetSkillName(SkillId id) => id switch
        {
            SkillId.QuickCut => "Quick Cut",
            SkillId.Thrust => "Thrust",
            SkillId.Sweep => "Sweep",
            SkillId.GroundBurst => "Ground Burst",
            SkillId.SwordThrow => "Sword Throw",
            SkillId.LightningRush => "Lightning Rush",
            SkillId.BlazeCharge => "Blaze Charge",
            SkillId.FrozenOrb => "Frozen Orb",
            SkillId.HolyGeyser => "Holy Geyser",
            _ => "Empty slot"
        };

        internal enum MotionMode { Stationary, Projectile, Dash }

        [Serializable]
        internal sealed class SkillView
        {
            private const int SwordThrowVariantCount = 5;

            [SerializeField] private SkillId _id;
            [SerializeField] private AnimationClip _animation;
            [SerializeField] private AnimationClip[] _variants;
            [SerializeField] private string _rangeHint;
            [SerializeField] private int _damage1;
            [SerializeField] private int _damage2;
            [SerializeField] private int _damage3;
            [SerializeField, Range(0f, 1f)] private float _activeStart;
            [SerializeField, Range(0f, 1f)] private float _activeEnd;
            [SerializeField] private Vector3 _center;
            [SerializeField] private Vector3 _size;
            [SerializeField] private MotionMode _motionMode;
            [SerializeField, Min(0f)] private float _travelDistance;
            [SerializeField] private ParticleSystem _effect;
            [SerializeField] private GameObject _effectRoot;
            [SerializeField, Range(0f, 1f)] private float _effectAt;
            [SerializeField, Min(0f)] private float _effectHeight = .12f;

            internal SkillId Id => _id;
            internal AnimationClip Animation => _animation;
            internal AnimationClip[] Variants => _variants ?? Array.Empty<AnimationClip>();
            internal string RangeHint => _rangeHint;
            internal Vector3 Center => _center;
            internal Vector3 Size => _size;
            internal MotionMode Mode => _motionMode;
            internal float TravelDistance => _travelDistance;
            internal GameObject EffectRoot => _effectRoot ? _effectRoot : _effect ? _effect.gameObject : null;
            internal bool RequiresEffect => _id >= SkillId.SwordThrow || _effect || _effectRoot;
            internal float EffectAt => _effectAt;
            internal float EffectHeight => _effectHeight;

            internal SkillDefinition Definition => new SkillDefinition(_id, _damage1, _damage2, _damage3,
                _activeStart, _activeEnd, _animation.length);

            internal AnimationClip ResolveAnimation(bool grounded, float pitch, out float travelPitch)
            {
                travelPitch = 0f;
                if (_id != SkillId.SwordThrow || Variants.Length != SwordThrowVariantCount)
                    return _animation;

                if (pitch <= -10f)
                {
                    travelPitch = -20f;
                    return grounded ? _variants[0] : _variants[3];
                }

                if (grounded)
                    return _animation;

                if (pitch >= 30f)
                {
                    travelPitch = 30f;
                    return _variants[4];
                }

                return _variants[2];
            }

            internal bool IsValid => _animation && Definition.IsValid && !string.IsNullOrWhiteSpace(_rangeHint) &&
                Finite(_center.x) && Finite(_center.y) && Finite(_center.z) &&
                _size.x > 0f && _size.y > 0f && _size.z > 0f && Finite(_size.x) && Finite(_size.y) && Finite(_size.z) &&
                (uint)_motionMode <= (uint)MotionMode.Dash &&
                (_motionMode == MotionMode.Stationary ? _travelDistance == 0f : _travelDistance > 0f && Finite(_travelDistance)) &&
                (_id != SkillId.SwordThrow || Variants.Length == SwordThrowVariantCount) &&
                (!RequiresEffect || (EffectRoot && _effectAt >= _activeStart && _effectAt <= _activeEnd && _effectHeight >= 0f &&
                    Finite(_effectHeight)));

            private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
