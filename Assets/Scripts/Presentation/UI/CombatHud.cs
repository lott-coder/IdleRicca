using System;
using System.Globalization;
using IdleRicca.Application.Gameplay;
using IdleRicca.Presentation.Gameplay;
using TMPro;
using UnityEngine;

namespace IdleRicca.Presentation.UI
{
    /// <summary>Draws the Demo combat state through the HUD's serialized UGUI references.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(200)]
    public sealed class CombatHud : MonoBehaviour
    {
        private const float HitPulseSeconds = 0.18f;
        private const float HitPulseScale = 0.16f;

        [SerializeField] private RiccaPlayer _player;
        [SerializeField] private GameObject _statusRoot;
        [SerializeField] private RectTransform _healthFill;
        [SerializeField] private TextMeshProUGUI _healthValue;
        [SerializeField] private RectTransform _manaFill;
        [SerializeField] private TextMeshProUGUI _manaValue;
        [SerializeField] private GameObject _comboRoot;
        [SerializeField] private RectTransform _comboNumber;
        [SerializeField] private TextMeshProUGUI _comboValue;
        [SerializeField] private RectTransform _comboTimerFill;
        [SerializeField] private CanvasGroup _comboFlash;

        // Two non-negative int values and a " / " separator.
        private readonly char[] _numberBuffer = new char[23];
        private int _displayedHp = -1;
        private int _displayedMaxHp = -1;
        private int _displayedMp = -1;
        private int _displayedMaxMp = -1;
        private int _displayedCombo;
        private ulong _lastHitSequence;
        private double _pulseStarted;

        private void Awake()
        {
            if (_player && _statusRoot && _healthFill && _healthValue && _comboRoot &&
                _comboNumber && _comboValue && _comboTimerFill)
            {
                return;
            }

            Debug.LogError("Combat HUD requires Ricca and all of its display references.", this);
            Hide();
            enabled = false;
        }

        private void OnEnable()
        {
            _lastHitSequence = 0;
            Hide();
        }

        private void OnDisable() => Hide();

        private void LateUpdate()
        {
            if (!_player || !_player.TryGetCombatStatus(out var status))
            {
                Hide();
                return;
            }

            if (!_statusRoot.activeSelf)
                _statusRoot.SetActive(true);

            if (status.CurrentHp != _displayedHp || status.MaxHp != _displayedMaxHp)
            {
                _displayedHp = status.CurrentHp;
                _displayedMaxHp = status.MaxHp;
                DrawResource(_healthFill, _healthValue, status.CurrentHp, status.MaxHp);
            }

            if (_manaFill && _manaValue && (status.CurrentMp != _displayedMp || status.MaxMp != _displayedMaxMp))
            {
                _displayedMp = status.CurrentMp;
                _displayedMaxMp = status.MaxMp;
                DrawResource(_manaFill, _manaValue, status.CurrentMp, status.MaxMp);
            }

            if (status.ComboCount < 2)
            {
                HideCombo();
                return;
            }

            DrawCombo(status);
        }

        private void DrawResource(RectTransform fill, TextMeshProUGUI value, int current, int maximum)
        {
            SetFill(fill, maximum > 0 ? (float)current / maximum : 0f);

            current.TryFormat(_numberBuffer, out int currentLength, provider: CultureInfo.InvariantCulture);
            _numberBuffer[currentLength] = ' ';
            _numberBuffer[currentLength + 1] = '/';
            _numberBuffer[currentLength + 2] = ' ';
            int maxStart = currentLength + 3;
            maximum.TryFormat(_numberBuffer.AsSpan(maxStart), out int maxLength, provider: CultureInfo.InvariantCulture);
            value.SetCharArray(_numberBuffer, 0, maxStart + maxLength);
        }

        private void DrawCombo(PlayerCombatStatus status)
        {
            if (!_comboRoot.activeSelf)
                _comboRoot.SetActive(true);

            if (_displayedCombo != status.ComboCount)
            {
                _displayedCombo = status.ComboCount;
                _displayedCombo.TryFormat(_numberBuffer, out int length, provider: CultureInfo.InvariantCulture);
                _comboValue.SetCharArray(_numberBuffer, 0, length);
            }

            double now = Time.unscaledTimeAsDouble;
            if (_lastHitSequence != status.HitSequence)
            {
                _lastHitSequence = status.HitSequence;
                _pulseStarted = now;
            }

            float pulse = Mathf.Clamp01(1f - (float)(now - _pulseStarted) / HitPulseSeconds);
            float emphasis = pulse * pulse;
            if (_comboFlash && _comboFlash.alpha != emphasis)
                _comboFlash.alpha = emphasis;

            float scale = 1f + HitPulseScale * emphasis;
            if (_comboNumber.localScale.x != scale)
                _comboNumber.localScale = new Vector3(scale, scale, 1f);

            SetFill(_comboTimerFill, (float)(status.ComboSecondsRemaining / PlayerActions.ComboWindowSeconds));
        }

        private void Hide()
        {
            if (_statusRoot && _statusRoot.activeSelf)
                _statusRoot.SetActive(false);
            HideCombo();
        }

        private void HideCombo()
        {
            if (_comboRoot && _comboRoot.activeSelf)
                _comboRoot.SetActive(false);
        }

        private static void SetFill(RectTransform fill, float fraction)
        {
            if (fill.localScale.x != fraction)
                fill.localScale = new Vector3(fraction, 1f, 1f);
        }
    }
}
