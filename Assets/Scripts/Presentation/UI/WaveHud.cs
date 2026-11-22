using IdleRicca.Application.Gameplay;
using IdleRicca.Domain.Gameplay;
using IdleRicca.Presentation.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Phase = IdleRicca.Application.Gameplay.WaveStatus.RunPhase;

namespace IdleRicca.Presentation.UI
{
    /// <summary>显示波次和选择面板，通过场景入口提交按钮意图。</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(210)]
    public sealed class WaveHud : MonoBehaviour
    {
        [SerializeField] private EnemySpawner _spawner;
        [SerializeField] private GameObject _waveBadge;
        [SerializeField] private TMP_Text _waveCounter;
        [SerializeField] private GameObject _rewardPanel;
        [SerializeField] private TMP_Text _rewardTitle;
        [SerializeField] private Button[] _rewardButtons;
        [SerializeField] private TMP_Text[] _rewardLabels;
        [SerializeField] private HudIconSet _icons;
        [SerializeField] private Image[] _rewardIcons;
        [SerializeField] private Color _finalRewardTitleColor = new Color32(245, 207, 141, 255);
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private TMP_Text _resultTitle;
        [SerializeField] private Image _resultAccent;
        [SerializeField] private Color _winTitleColor = new Color32(245, 207, 141, 255);
        [SerializeField] private Color _lossTitleColor = new Color32(240, 158, 150, 255);
        [SerializeField] private Button _restart;
        [SerializeField] private GameObject _restartError;

        private bool _configured;
        private int _shownWave;
        private ulong _shownOffer;
        private bool _hasStatus;
        private Phase _shownPhase;
        private bool _restartReady;
        private bool _restartDelayed;
        private Color _rewardTitleColor;
        private Color _resultTitleColor;

        private void Awake()
        {
            _configured = _spawner && _waveBadge && _waveCounter && _rewardPanel && _rewardTitle && _resultPanel &&
                _resultTitle && _restart && _restartError && _rewardButtons?.Length == WaveStatus.OfferSize &&
                _rewardLabels?.Length == WaveStatus.OfferSize;
            if (_configured)
            {
                for (int i = 0; i < WaveStatus.OfferSize; i++)
                    _configured &= _rewardButtons[i] && _rewardLabels[i];
            }

            if (!_configured)
            {
                Debug.LogError("Wave HUD requires a spawner, three linked rewards and a result panel.", this);
                enabled = false;
                return;
            }

            _rewardTitleColor = _rewardTitle.color;
            _resultTitleColor = _resultTitle.color;
        }

        private void OnEnable()
        {
            if (!_configured)
                return;

            _rewardButtons[0].onClick.AddListener(ChooseLeft);
            _rewardButtons[1].onClick.AddListener(ChooseMiddle);
            _rewardButtons[2].onClick.AddListener(ChooseRight);
            _restart.onClick.AddListener(Restart);
            _hasStatus = false;
        }

        private void OnDisable()
        {
            if (!_configured)
                return;

            _rewardButtons[0].onClick.RemoveListener(ChooseLeft);
            _rewardButtons[1].onClick.RemoveListener(ChooseMiddle);
            _rewardButtons[2].onClick.RemoveListener(ChooseRight);
            _restart.onClick.RemoveListener(Restart);
            Hide();
            _hasStatus = false;
        }

        private void LateUpdate()
        {
            if (!_spawner || !_spawner.TryGetWaveStatus(out WaveStatus status))
            {
                if (_hasStatus)
                    Hide();
                _hasStatus = false;
                return;
            }

            bool restartReady = _spawner.IsWaveRestartReady;
            bool restartDelayed = _spawner.IsWaveRestartDelayed;
            if (!_hasStatus || _shownWave != status.Number)
            {
                _waveBadge.SetActive(true);
                _waveCounter.SetText("{0}/{1}", status.Number, WaveStatus.TotalWaves);
                _shownWave = status.Number;
            }

            if (!_hasStatus || _shownOffer != status.OfferId)
            {
                _shownOffer = status.OfferId;
                _rewardPanel.SetActive(_shownOffer != 0);
                if (_shownOffer != 0)
                {
                    bool finalReward = status.Number == WaveStatus.TotalWaves;
                    _rewardTitle.text = finalReward ? "Final Reward" : "Choose a Reward";
                    _rewardTitle.color = finalReward ? _finalRewardTitleColor : _rewardTitleColor;
                    for (int i = 0; i < WaveStatus.OfferSize; i++)
                        ShowReward(status.GetReward(i), i);
                }
            }

            if (!_hasStatus || _shownPhase != status.Phase || _restartReady != restartReady ||
                _restartDelayed != restartDelayed)
            {
                _resultPanel.SetActive(restartReady || status.Phase == Phase.Restarting);
                _resultTitle.text = status.Phase == Phase.Won ? "Run Complete" :
                    restartDelayed ? "Reload delayed" :
                    status.Phase == Phase.Restarting ? "Restarting..." : "Run Over";
                _resultTitle.color = status.Phase == Phase.Won ? _winTitleColor :
                    status.Phase == Phase.Defeated || restartDelayed ? _lossTitleColor : _resultTitleColor;
                if (_resultAccent)
                    _resultAccent.color = _resultTitle.color;
                _restart.interactable = restartReady;
                _restartError.SetActive(false);
                _shownPhase = status.Phase;
                _restartReady = restartReady;
                _restartDelayed = restartDelayed;
            }

            _hasStatus = true;
        }

        private void ShowReward(WaveReward reward, int index)
        {
            TMP_Text label = _rewardLabels[index];
            Sprite sprite;
            switch (reward.Kind)
            {
                case WaveReward.RewardKind.Health:
                    label.SetText("Health Lv.{0}\n+{1} Max & Current HP", reward.Level, reward.Amount);
                    sprite = _icons ? _icons.Health : null;
                    break;
                case WaveReward.RewardKind.Attack:
                    label.SetText("Attack Lv.{0}\n+{1} Normal Attack", reward.Level, reward.Amount);
                    sprite = _icons ? _icons.Attack : null;
                    break;
                default:
                    if (_spawner.TryGetSkillRewardPreview(_shownOffer, index, out SkillRewardPreview preview))
                    {
                        bool isUpgrade = preview.PreviousLevel > 0;
                        label.SetText(SkillRewardFormat(preview.Skill, isUpgrade), preview.Level, preview.PreviousLevel);
                        sprite = _icons ? _icons.GetSkillIcon(preview.Skill) : null;
                    }
                    else
                    {
                        label.SetText("Skill Lv.{0}\nPlaceholder", reward.Level);
                        sprite = _icons ? _icons.GenericSkill : null;
                    }
                    break;
            }

            if (_rewardIcons != null && index < _rewardIcons.Length && _rewardIcons[index])
            {
                Image icon = _rewardIcons[index];
                icon.sprite = sprite;
                icon.enabled = sprite;
            }
        }

        private static string SkillRewardFormat(SkillId id, bool isUpgrade)
        {
            switch (id)
            {
                case SkillId.QuickCut:
                    return isUpgrade ? "Quick Cut\nLv.{1} > Lv.{0}" : "Learn Quick Cut\nLv.{0}";
                case SkillId.Thrust:
                    return isUpgrade ? "Thrust\nLv.{1} > Lv.{0}" : "Learn Thrust\nLv.{0}";
                case SkillId.Sweep:
                    return isUpgrade ? "Sweep\nLv.{1} > Lv.{0}" : "Learn Sweep\nLv.{0}";
                case SkillId.SwordThrow:
                    return isUpgrade ? "Sword Throw\nLv.{1} > Lv.{0}" : "Learn Sword Throw\nLv.{0}";
                case SkillId.LightningRush:
                    return isUpgrade ? "Lightning Rush\nLv.{1} > Lv.{0}" : "Learn Lightning Rush\nLv.{0}";
                case SkillId.BlazeCharge:
                    return isUpgrade ? "Blaze Charge\nLv.{1} > Lv.{0}" : "Learn Blaze Charge\nLv.{0}";
                case SkillId.FrozenOrb:
                    return isUpgrade ? "Frozen Orb\nLv.{1} > Lv.{0}" : "Learn Frozen Orb\nLv.{0}";
                case SkillId.HolyGeyser:
                    return isUpgrade ? "Holy Geyser\nLv.{1} > Lv.{0}" : "Learn Holy Geyser\nLv.{0}";
                default:
                    return isUpgrade ? "Ground Burst\nLv.{1} > Lv.{0}" : "Learn Ground Burst\nLv.{0}";
            }
        }

        private void Hide()
        {
            _waveBadge.SetActive(false);
            _resultPanel.SetActive(false);
            _rewardPanel.SetActive(false);
        }

        private void ChooseLeft() => Choose(0);
        private void ChooseMiddle() => Choose(1);
        private void ChooseRight() => Choose(2);

        private void Choose(int index)
        {
            if (_spawner)
                _spawner.TryChooseWaveReward(_shownOffer, index);
        }

        private void Restart()
        {
            if (_spawner && _spawner.IsWaveRestartReady)
                _restartError.SetActive(!_spawner.TryRestartWaveRun());
        }
    }
}
