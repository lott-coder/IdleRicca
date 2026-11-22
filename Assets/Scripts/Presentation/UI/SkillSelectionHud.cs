using IdleRicca.Application.Gameplay;
using IdleRicca.Domain.Gameplay;
using IdleRicca.Presentation.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleRicca.Presentation.UI
{
    /// <summary>从当前快照显示四槽并提交有效按钮意图。</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(205)]
    public sealed class SkillSelectionHud : MonoBehaviour
    {
        [SerializeField] private RiccaPlayer _ricca;
        [SerializeField] private GameObject _panel;
        [SerializeField] private TMP_Text _hint;
        [SerializeField] private Button[] _buttons;
        [SerializeField] private TMP_Text[] _names;
        [SerializeField] private TMP_Text[] _details;
        [SerializeField] private string _readyHint;
        [SerializeField] private string _emptyHint;
        [SerializeField] private string _manaHint;
        [SerializeField] private string _unavailableHint;
        [SerializeField] private string _unavailableSlot;

        private bool _configured;
        private bool _bound;
        private ulong _shownSelection;
        private int _shownAvailableSlots;
        private bool _shownManaReady;

        private void Awake()
        {
            _configured = _ricca && _panel && _hint && _buttons?.Length == SkillStatus.SlotCount &&
                _names?.Length == SkillStatus.SlotCount && _details?.Length == SkillStatus.SlotCount;

            if (_configured)
            {
                for (int i = 0; i < SkillStatus.SlotCount; i++)
                    _configured &= _buttons[i] && _names[i] && _details[i];
            }

            if (!_configured)
            {
                Debug.LogError("Skill HUD requires Ricca and four fully bound slots.", this);
                enabled = false;
                return;
            }

            _panel.SetActive(false);
        }

        private void OnEnable()
        {
            if (!_configured)
                return;

            _bound = _ricca.BindSkillHud(this);
            if (!_bound)
            {
                Debug.LogError("Ricca can only bind one active skill HUD in the same scene.", this);
                enabled = false;
                return;
            }

            _buttons[0].onClick.AddListener(ChooseFirst);
            _buttons[1].onClick.AddListener(ChooseSecond);
            _buttons[2].onClick.AddListener(ChooseThird);
            _buttons[3].onClick.AddListener(ChooseFourth);
        }

        private void OnDisable()
        {
            if (!_bound)
                return;

            _bound = false;
            _buttons[0].onClick.RemoveListener(ChooseFirst);
            _buttons[1].onClick.RemoveListener(ChooseSecond);
            _buttons[2].onClick.RemoveListener(ChooseThird);
            _buttons[3].onClick.RemoveListener(ChooseFourth);
            if (_ricca)
                _ricca.UnbindSkillHud(this);
            ShowClosed();
        }

        private void LateUpdate()
        {
            if (!_ricca || !_ricca.TryGetSkillStatus(out SkillStatus status) || !status.IsSelecting)
            {
                if (_shownSelection != 0)
                    ShowClosed();
                return;
            }

            if (_shownSelection == status.SelectionId && _shownAvailableSlots == status.AvailableSlots &&
                _shownManaReady == status.HasMana)
                return;

            _shownSelection = status.SelectionId;
            _shownAvailableSlots = status.AvailableSlots;
            _shownManaReady = status.HasMana;
            _hint.text = status.IsUsingBulletTime ? _readyHint :
                !status.HasMana ? _manaHint : status.HasEquippedSkills ? _unavailableHint : _emptyHint;
            for (int i = 0; i < SkillStatus.SlotCount; i++)
            {
                SkillId id = status.GetSkillId(i);
                _buttons[i].interactable = status.CanCastSlot(i);
                _names[i].text = RiccaSkillSettings.GetSkillName(id);
                _details[i].text = id != SkillId.None && (status.AvailableSlots & (1 << i)) == 0
                    ? _unavailableSlot : _ricca.GetSkillDetails(id, status.GetLevel(i));
            }

            _panel.SetActive(true);
        }

        private void ShowClosed()
        {
            _shownSelection = 0;
            _panel.SetActive(false);
        }

        private void ChooseFirst() => Choose(0);
        private void ChooseSecond() => Choose(1);
        private void ChooseThird() => Choose(2);
        private void ChooseFourth() => Choose(3);

        private void Choose(int slot)
        {
            if (_ricca && _ricca.TryChooseSkill(_shownSelection, slot))
                ShowClosed();
        }
    }
}
