using System.Collections.Generic;
using System.Threading.Tasks;
using Sabrevois.Gameplay;
using Sabrevois.Level;
using UnityEngine;
using UnityEngine.UIElements;
using Zenject;

namespace Sabrevois.UI
{
    /// <summary>
    /// Chat bar on the HUD: type a line, press Enter, the NPC you are looking at (or the
    /// closest one) answers with the text model and speaks it through Piper.
    /// </summary>
    public class NpcChatUI : MonoBehaviour
    {
        [SerializeField] private UIDocument _document;
        [SerializeField] private int _maxLogLines = 6;

        [Inject] private NPCBarkService _barks;

        private Label _log;
        private TextField _input;
        private Button _close;
        private InputRouter _inputRouter;
        private readonly List<string> _lines = new();

        private void Awake()
        {
            if (_document == null)
                _document = GetComponent<UIDocument>();

            if (_document == null)
            {
                Debug.LogError("[NpcChatUI] No UIDocument on this GameObject.");
                enabled = false;
                return;
            }

            VisualElement root = _document.rootVisualElement;
            _log = root.Q<Label>("chat-log");
            _input = root.Q<TextField>("chat-input");
            _close = root.Q<Button>("chat-close");

            if (_input == null)
            {
                Debug.LogError("[NpcChatUI] 'chat-input' missing from UI_HUD.uxml.");
                enabled = false;
                return;
            }

            if (_close != null)
                _close.clicked += Defocus;

            _input.textEdition.placeholder = "Say something to the traveller near you... (Enter to send)";
            _input.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
            _input.RegisterCallback<FocusInEvent>(_ => SetGameplayInput(false));
            _input.RegisterCallback<FocusOutEvent>(_ => SetGameplayInput(true));
        }

        private void Start()
        {
            _inputRouter = FindFirstObjectByType<InputRouter>();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                evt.PreventDefault();
                Defocus();
                return;
            }

            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter)
                return;

            evt.StopPropagation();
            evt.PreventDefault();
            _ = SubmitAsync();
        }

        /// <summary>Releases the field and gives movement/attack input back to the player.</summary>
        public void Defocus()
        {
            _input?.Blur();
            SetGameplayInput(true);
        }

        private async Task SubmitAsync()
        {
            string text = _input.value?.Trim();
            if (string.IsNullOrEmpty(text))
                return;

            _input.SetValueWithoutNotify(string.Empty);
            Append($"You: {text}");

            if (_barks == null)
            {
                Append("(chat service unavailable)");
                return;
            }

            Health target = _barks.FindChatTarget();
            if (target == null)
            {
                Append("No one is close enough to answer.");
                return;
            }

            var (reply, _) = await _barks.ChatAsync(target, text);

            Append(string.IsNullOrWhiteSpace(reply)
                ? $"{target.name}: ..."
                : $"{target.name}: {reply}");
        }

        private void Append(string line)
        {
            _lines.Add(line);
            while (_lines.Count > _maxLogLines)
                _lines.RemoveAt(0);

            if (_log != null)
                _log.text = string.Join("\n", _lines);
        }

        private void SetGameplayInput(bool enabled)
        {
            if (_inputRouter != null)
                _inputRouter.SetGameplayEnabled(enabled);
        }
    }
}
