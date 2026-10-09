using System.Linq;

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// On-screen virtual keyboard for typing into a <see cref="TMP_InputField"/> with a gamepad — required
    /// on Steam Deck (Valve's "Deck Verified" checklist fails a game whose text fields have no way to type with
    /// only a controller) and useful on any platform where a physical keyboard is Xbox/PlayStation-pad users.
    /// <see cref="RewiredInputManager"/> shows/hides it automatically whenever a <c>TMP_InputField</c> is
    /// selected while the active input is a joystick; no per-field wiring is required.
    ///
    /// Ported from this project's own <c>Assets/OSK</c> (used across several Green Sauce Games titles), renamed
    /// to avoid clashing with that global-namespace script, and with the hard MasterAudio dependency replaced by
    /// <see cref="OnKeyPressed"/> so the host game can hook its own SFX (or none) — this package never takes a
    /// hard third-party dependency. The optional <c>Animator</c> "Hide" bool trigger from the original is kept for
    /// projects that bring their own show/hide animation; without one, it just toggles active state.
    /// </summary>
    public class RewiredOnScreenKeyboard : MonoBehaviour
    {
        [Tooltip("The input field currently receiving keystrokes.")]
        public TMP_InputField focus;

        public bool showNumeric = false;

        [Header("Theming (optional)")]
        public Color32 textColor = Color.black;
        public Color32 mainColor = Color.white;
        public Color32 specialColor = Color.gray;
        public Color32 backgroundColor = Color.white;
        [Tooltip("Tint of the key currently selected with the gamepad (D-pad / stick).")]
        public Color32 selectedColor = new Color32(51, 119, 255, 255);
        [Tooltip("Colour of the OK (submit) key.")]
        public Color32 submitColor = new Color32(46, 160, 90, 255);
        [Tooltip("Colour of the CAPS key while Shift or Caps Lock is on.")]
        public Color32 capsActiveColor = new Color32(240, 170, 40, 255);
        [Tooltip("Corner size of the rounded keys, in canvas units. Independent of the Canvas 'Reference Pixels Per Unit'.")]
        [Min(1f)] public float cornerRadius = 16f;
        [Tooltip("Give the keys and the background soft rounded corners (a generated sprite; ignored when a sprite is assigned below).")]
        public bool roundedKeys = true;
        [Tooltip("Optional font for every key label. Leave empty to keep each label's own font (the project default TMP font can carry an outline that looks wrong on keys).")]
        public TMP_FontAsset font;
        public Sprite mainSprite;
        public Sprite specialSprite;

        [Header("Debug")]
        [Tooltip("Logs every key event (pointer down/up/click, submit, selection, caps) with the frame and the selected object, to track double or missed clicks. Only active in the Editor and development builds.")]
        public bool debugLogging = true;

        [Header("Behaviour")]
        [Tooltip("Hide the keyboard after the OK key submits the field.")]
        public bool hideOnSubmit = true;
        [Tooltip("When the keyboard opens, move the gamepad selection to the first key so it can be navigated immediately.")]
        public bool selectFirstKeyOnShow = true;
        [Tooltip("Start each sentence with a capital letter: Shift is on while the field is empty, and turns off after the first letter.")]
        public bool autoCapitalize = true;

        [Header("Layout")]
        [Tooltip("panels[0] = lowercase letters, [1] = uppercase/caps, [2] = optional extra symbol panel, [3] = numeric (toggled by ShowNumeric).")]
        public GameObject[] panels;
        [Tooltip("Character keys: each needs a TMP_Text child holding the glyph to type.")]
        public GameObject[] keys;
        [Tooltip("Non-character keys (backspace, enter, caps, hide, switch panel...) driven by WriteSpecialKey.")]
        public GameObject[] specialKeys;

        [Tooltip("Invoked on every key press (character or special). Wire your own SFX here.")]
        public UnityEvent OnKeyPressed;

        /// <summary>Set by the setup generator; lets the Inspector offer an upgrade for keyboards built by an older layout.</summary>
        [HideInInspector] public int layoutVersion;

        [HideInInspector] public bool isActive;
        [HideInInspector] public bool capsEnabled;

        /// <summary>True when caps is locked (stays on until CAPS is pressed again); false when it is a one-letter Shift.</summary>
        public bool CapsLocked { get; private set; }

        private Animator _animator;
        private TMP_InputField _dismissedFor;

        /// <summary>The field the player closed the keyboard on; it is not reopened until the selection moves to another object.</summary>
        public TMP_InputField DismissedFor => _dismissedFor;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
        }

        // Registering on enable (and unregistering on destroy, never on disable) lets the manager find a
        // keyboard spawned/activated at runtime. A keyboard that starts inactive — like the one the setup
        // generator builds — never runs OnEnable, so RewiredInputManager.DiscoverOnScreenKeyboards() also
        // scans the scene for it. Do NOT unregister on OnDisable: hiding the keyboard deactivates the
        // GameObject, and it must stay registered so the manager can show it again later.
        private void OnEnable() => RewiredInputManager.RegisterOnScreenKeyboard(this);

        private void OnDestroy() => RewiredInputManager.UnregisterOnScreenKeyboard(this);

        private void Update()
        {
            // The form that owned the field was closed (or destroyed): the keyboard has nothing left to type into.
            if (isActive && (focus == null || !focus.isActiveAndEnabled))
            {
                DebugLog("field is gone (form closed): hiding");
                SetActive(false);
            }
        }

        private void Start()
        {
            ShowNumeric(showNumeric);
            ApplyTheme();
        }

        /// <summary>Applies every theming field to the keys. Also callable from the Editor to preview the look without Play Mode.</summary>
        [ContextMenu("Apply Theme")]
        public void ApplyTheme()
        {
            SetTextColor(textColor);
            SetMainColor(mainColor);
            SetSpecialColor(specialColor);
            SetBackgroundColor(backgroundColor);
            SetMainSprite(mainSprite);
            SetSpecialSprite(specialSprite);
            ApplyRoundedLook();
            ApplyCornerSizes();
            SetFont(font);
            ColorSpecialKeys();
        }

        /// <summary>Writes one line describing a keyboard event. No-op outside the Editor and development builds, or when <see cref="debugLogging"/> is off.</summary>
        internal void DebugLog(string message)
        {
            if (!debugLogging || !(Application.isEditor || Debug.isDebugBuild)) return;

            var eventSystem = EventSystem.current;
            var selected = eventSystem != null && eventSystem.currentSelectedGameObject != null ? eventSystem.currentSelectedGameObject.name : "none";
            Debug.Log($"[OSK f{Time.frameCount} t{Time.unscaledTime:F2}] {message} | selected={selected} active={isActive} shift={capsEnabled} lock={CapsLocked} focus={(focus != null ? focus.name : "none")}", this);
        }

        /// <summary>Soft corners from a generated sprite. Runtime only: a generated sprite cannot be serialized into a scene.</summary>
        private void ApplyRoundedLook()
        {
            if (!roundedKeys || !Application.isPlaying)
            {
                DebugLog($"rounded look skipped (roundedKeys={roundedKeys}, playing={Application.isPlaying})");
                return;
            }

            try
            {
                var rounded = RewiredOnScreenKeyboardSprites.Rounded;
                ForEachImage(keys, img => UseSliced(img, mainSprite != null ? mainSprite : rounded));
                ForEachImage(specialKeys, img => UseSliced(img, specialSprite != null ? specialSprite : rounded));

                var background = GetComponent<Image>();
                if (background != null) UseSliced(background, rounded);
                DebugLog($"rounded look applied: sprite={(rounded != null ? rounded.name : "null")} border={(rounded != null ? rounded.border.ToString() : "-")} keys={(keys != null ? keys.Length : 0)}");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[RewiredHelper] Could not apply the rounded on-screen keyboard look: {ex.Message}", this);
            }
        }

        /// <summary>Re-applies the corner size to every sliced key/background (also in the Editor, for sprites assigned by the generator).</summary>
        private void ApplyCornerSizes()
        {
            ForEachImage(keys, img => ApplyCornerSize(img, img.sprite));
            ForEachImage(specialKeys, img => ApplyCornerSize(img, img.sprite));

            var background = GetComponent<Image>();
            if (background != null) ApplyCornerSize(background, background.sprite);
        }

        private void UseSliced(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            ApplyCornerSize(image, sprite);
        }

        /// <summary>
        /// A sliced sprite's border is drawn at (sprite pixels per unit / Canvas reference pixels per unit) per sprite pixel. A project that
        /// sets the Canvas "Reference Pixels Per Unit" to 1 therefore shrinks a 20 px border to a fraction of a pixel and the corners look square.
        /// The multiplier cancels that, so the corner is <see cref="cornerRadius"/> canvas units whatever the project uses.
        /// </summary>
        private void ApplyCornerSize(Image image, Sprite sprite)
        {
            if (image == null || sprite == null || sprite.border.x <= 0f) return;

            var canvas = image.GetComponentInParent<Canvas>(true);
            var reference = canvas != null && canvas.referencePixelsPerUnit > 0f ? canvas.referencePixelsPerUnit : 100f;
            var spritePpu = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 100f;
            var wantedPpu = sprite.border.x / Mathf.Max(1f, cornerRadius);

            image.pixelsPerUnitMultiplier = wantedPpu / (spritePpu / reference);
        }

        /// <summary>OK gets the submit colour and CAPS shows its state, on top of the shared special-key colour.</summary>
        private void ColorSpecialKeys()
        {
            if (specialKeys == null) return;

            foreach (var go in specialKeys)
            {
                var key = go != null ? go.GetComponent<RewiredOnScreenKeyboardKey>() : null;
                if (key == null) continue;

                if (key.specialKey == 1)
                {
                    key.SetBaseColor(submitColor);
                }
                else if (key.specialKey == 2)
                {
                    key.SetBaseColor(capsEnabled ? capsActiveColor : specialColor);
                    if (key.label != null) key.label.text = !capsEnabled ? "CAPS" : CapsLocked ? "CAPS LOCK" : "SHIFT";
                }
            }
        }

        public void ShowNumeric(bool show)
        {
            if (panels != null && panels.Length > 3 && panels[3] != null)
                panels[3].SetActive(show);
            showNumeric = show;
        }

        public void SetTextColor(Color32 color)
        {
            ForEachLabel(keys, t => t.color = color);
            ForEachLabel(specialKeys, t => t.color = color);
        }

        public void SetMainColor(Color32 color) => ForEachKeyColor(keys, color);

        public void SetSpecialColor(Color32 color) => ForEachKeyColor(specialKeys, color);

        public void SetBackgroundColor(Color32 color)
        {
            var background = GetComponent<Image>();
            if (background != null) background.color = color;
        }

        public void SetFont(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null) return;
            ForEachLabel(keys, t => t.font = fontAsset);
            ForEachLabel(specialKeys, t => t.font = fontAsset);
        }

        public void SetMainSprite(Sprite sprite)
        {
            if (sprite == null) return;
            ForEachImage(keys, img => AssignSprite(img, sprite));
        }

        public void SetSpecialSprite(Sprite sprite)
        {
            if (sprite == null) return;
            ForEachImage(specialKeys, img => AssignSprite(img, sprite));
        }

        /// <summary>Assigns a sprite and slices it when it has borders (a rounded-corner sprite must not be stretched).</summary>
        private void AssignSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            if (sprite.border == Vector4.zero) return;

            image.type = Image.Type.Sliced;
            ApplyCornerSize(image, sprite);
        }

        public void SetFocus(TMP_InputField inputField) => focus = inputField;

        /// <summary>Shows the keyboard already bound to <paramref name="inputField"/>, cursor at the end.</summary>
        public void SetActiveFocus(TMP_InputField inputField)
        {
            focus = inputField;
            SetActive(true);

            // Caret at the end WITHOUT extending the selection: Select()/ActivateInputField() leave the whole text highlighted.
            if (focus != null) focus.MoveTextEnd(false);

            RefreshAutoCapitalization();
            SelectFirstKeyIfNeeded();
            DebugLog($"opened for '{(focus != null ? focus.name : "none")}'");
        }

        private GameObject _lastSelectedKey;

        /// <summary>Called by a key when the gamepad selects it, so the selection can return to the same key later.</summary>
        internal void NotifyKeySelected(GameObject key) => _lastSelectedKey = key;

        /// <summary>
        /// Gives the gamepad selection back to the keys. Game code often selects the input field again after the keyboard has opened
        /// (for example a form that calls <c>Select()</c> on its field half a second after showing); that would leave the keyboard open but
        /// unreachable, so <see cref="RewiredInputManager"/> calls this whenever the selection lands on the field while the keyboard is open.
        /// </summary>
        public void RestoreKeySelection()
        {
            if (isActive) SelectFirstKeyIfNeeded();
        }

        /// <summary>Opens the keyboard again on a field the player had closed it on (pressing A on the selected field).</summary>
        public void ReopenFor(TMP_InputField inputField)
        {
            _dismissedFor = null;
            SetActiveFocus(inputField);
        }

        /// <summary>True when <paramref name="selected"/> is this keyboard or one of its keys (the gamepad is navigating the keyboard).</summary>
        public bool ContainsSelection(GameObject selected) =>
            selected != null && selected.transform.IsChildOf(transform);

        /// <summary>Forgets a previous Close so the keyboard may open again for <paramref name="inputField"/>.</summary>
        public void ClearDismissed(TMP_InputField inputField)
        {
            if (_dismissedFor != null && _dismissedFor != inputField)
                _dismissedFor = null;
        }

        public void WriteKey(TMP_Text label)
        {
            OnKeyPressed?.Invoke();
            if (focus == null || label == null || focus.readOnly) return;

            var typed = FilterAccepted(label.text);
            if (typed.Length == 0) return;

            focus.text += typed;
            if (focus.characterLimit > 0 && focus.text.Length > focus.characterLimit)
                focus.text = focus.text.Substring(0, focus.characterLimit);

            // A one-letter Shift is spent by the letter it capitalised; Caps Lock stays on.
            if (capsEnabled && !CapsLocked && typed.Any(char.IsLetter))
                SetCaps(false);
        }

        /// <summary>0 = backspace, 1 = submit/enter, 2 = toggle caps, 3 = hide, 4/5/8 = switch panel, 6/7 = focus prev/next.</summary>
        public void WriteSpecialKey(int key)
        {
            OnKeyPressed?.Invoke();
            switch (key)
            {
                case 0: Backspace(); break;
                case 1: Submit(); break;
                case 2: CapsKeyPressed(); break;
                case 3: Close(); break;
                case 4: SetKeyboardType(1); break;
                case 5: SetKeyboardType(2); break;
                case 6: FocusPrevious(); break;
                case 7: FocusNext(); break;
                case 8: SetKeyboardType(0); break;
            }
        }

        public void Backspace()
        {
            if (focus == null || focus.readOnly || focus.text.Length == 0) return;
            focus.text = focus.text.Substring(0, focus.text.Length - 1);
            RefreshAutoCapitalization();
        }

        /// <summary>
        /// Submits the focused field, then triggers the same "Return pressed" dialog-confirm routing a
        /// physical Enter key gets from <see cref="RewiredInputManager.TriggerReturnConfirm"/> — so this
        /// key also fires the enclosing <see cref="Dialog.OkButton"/> (its "Default OK") when this keyboard
        /// is being used inside a modal dialog, not just the input field's own OnSubmit.
        /// </summary>
        public void Submit()
        {
            if (focus != null)
                focus.OnSubmit(new PointerEventData(EventSystem.current));

            RewiredInputManager.Instance?.TriggerReturnConfirm();

            if (hideOnSubmit) Close();
        }

        /// <summary>
        /// Hides the keyboard and hands the gamepad selection back to the field it was typing into. The field is
        /// remembered as dismissed so <see cref="RewiredInputManager"/> does not reopen the keyboard immediately
        /// (the field becomes selected again, which is what normally opens it).
        /// </summary>
        public void Close()
        {
            var field = focus;
            _dismissedFor = field;
            SetActive(false);
            DebugLog("closed");

            var eventSystem = EventSystem.current;
            if (eventSystem != null && field != null && field.gameObject.activeInHierarchy)
                eventSystem.SetSelectedGameObject(field.gameObject);
        }

        public void SetActive(bool show)
        {
            if (show)
            {
                if (!isActive)
                {
                    if (_animator != null)
                    {
                        _animator.Rebind();
                        _animator.enabled = true;
                    }
                    else
                    {
                        gameObject.SetActive(true);
                    }
                }
            }
            else if (isActive)
            {
                if (_animator != null)
                    _animator.SetBool("Hide", true);
                else
                    gameObject.SetActive(false);
            }

            isActive = show;
        }

        public void SetCaps(bool enabled)
        {
            ForEachLabel(keys, t => t.text = enabled ? t.text.ToUpperInvariant() : t.text.ToLowerInvariant());
            capsEnabled = enabled;
            if (!enabled) CapsLocked = false;
            ColorSpecialKeys();
        }

        public void SwitchCaps() => SetCaps(!capsEnabled);

        // Two presses of CAPS within this window lock caps (like double-tapping Shift on a phone).
        private const float CapsDoubleTapWindow = 0.45f;
        private float _lastCapsPressTime = float.NegativeInfinity;

        /// <summary>
        /// The CAPS key. One press toggles Shift on/off (it can already be on because of auto-capitalisation, in which case one
        /// press turns it off); pressing again right away locks caps; pressing while locked turns it off.
        /// </summary>
        public void CapsKeyPressed()
        {
            var now = Time.unscaledTime;
            var doubleTap = now - _lastCapsPressTime <= CapsDoubleTapWindow;
            _lastCapsPressTime = now;

            if (CapsLocked)
            {
                SetCaps(false);
            }
            else if (capsEnabled && doubleTap)
            {
                CapsLocked = true;
                ColorSpecialKeys();
            }
            else
            {
                SetCaps(!capsEnabled);
            }

            DebugLog($"CAPS pressed -> shift={capsEnabled} lock={CapsLocked} (doubleTap={doubleTap})");
        }

        /// <summary>Shift is on while the field is empty (first letter capitalised); never fights a Caps Lock the player chose.</summary>
        private void RefreshAutoCapitalization()
        {
            if (!autoCapitalize || CapsLocked || focus == null) return;

            var wantsShift = focus.text.Length == 0;
            if (wantsShift != capsEnabled) SetCaps(wantsShift);
        }

        public void FocusPrevious() => MoveFocus(s => s.FindSelectableOnLeft() ?? s.FindSelectableOnUp());

        public void FocusNext() => MoveFocus(s => s.FindSelectableOnRight() ?? s.FindSelectableOnDown());

        private void MoveFocus(System.Func<Selectable, Selectable> pick)
        {
            if (focus == null) return;

            var current = focus.GetComponent<Selectable>();
            var next = current != null ? pick(current) : null;
            if (next == null) return;

            var eventSystem = EventSystem.current;
            var nextField = next.GetComponent<TMP_InputField>();
            if (nextField != null)
            {
                nextField.OnPointerClick(new PointerEventData(eventSystem));
                focus = nextField;
            }
            eventSystem.SetSelectedGameObject(next.gameObject);
        }

        /// <summary>0 = lowercase, 1 = uppercase, 2 = the optional extra symbol panel.</summary>
        public void SetKeyboardType(int type)
        {
            if (panels == null) return;
            for (int i = 0; i < panels.Length && i < 3; i++)
            {
                if (panels[i] != null) panels[i].SetActive(i == type);
            }
        }

        /// <summary>Moves the gamepad selection onto the first key so the player can navigate the keyboard right away.</summary>
        private void SelectFirstKeyIfNeeded()
        {
            if (!selectFirstKeyOnShow || !Application.isPlaying || !gameObject.activeInHierarchy) return;

            // Not immediately: the input field that just opened this keyboard re-selects itself in its own LateUpdate
            // (TMP_InputField.ActivateInputField), which would steal the selection straight back from the first key.
            StartCoroutine(SelectFirstKeyAfterFrames(SelectionSettleFrames));
        }

        private const int SelectionSettleFrames = 2;

        private System.Collections.IEnumerator SelectFirstKeyAfterFrames(int frames)
        {
            for (var i = 0; i < frames; i++)
                yield return null;

            if (!isActive) yield break;

            // Only take the selection from the field (or nothing): if the player already moved onto a key, leave it alone.
            var eventSystem = EventSystem.current;
            var selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            var selectionIsFreeToMove = selected == null || (focus != null && selected == focus.gameObject);
            if (!selectionIsFreeToMove) yield break;

            SelectFirstKey();
        }

        private void SelectFirstKey()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null || keys == null) return;

            // Prefer the key the player was on, so the selection does not jump back to "1" every time.
            if (_lastSelectedKey != null && _lastSelectedKey.activeInHierarchy)
            {
                eventSystem.SetSelectedGameObject(_lastSelectedKey);
                DebugLog($"key selection restored to '{_lastSelectedKey.name}'");
                return;
            }

            foreach (var key in keys)
            {
                if (key == null || !key.activeInHierarchy) continue;

                var selectable = key.GetComponent<Selectable>();
                if (selectable == null || !selectable.IsInteractable()) continue;

                eventSystem.SetSelectedGameObject(key);
                DebugLog($"first key '{key.name}' selected");
                return;
            }
        }

        /// <summary>Keeps only the characters the focused field would accept (digits-only fields, custom validators).</summary>
        private string FilterAccepted(string text)
        {
            if (string.IsNullOrEmpty(text) || focus == null) return string.Empty;

            var accepted = new System.Text.StringBuilder(text.Length);
            var current = focus.text;
            foreach (var c in text)
            {
                if (Accepts(current, c))
                {
                    accepted.Append(c);
                    current += c;
                }
            }

            return accepted.ToString();
        }

        private bool Accepts(string currentText, char c)
        {
            if (focus.onValidateInput != null)
                return focus.onValidateInput(currentText, currentText.Length, c) != '\0';

            switch (focus.characterValidation)
            {
                case TMP_InputField.CharacterValidation.Digit:
                    return c >= '0' && c <= '9';
                case TMP_InputField.CharacterValidation.Integer:
                    return (c >= '0' && c <= '9') || (c == '-' && currentText.Length == 0);
                case TMP_InputField.CharacterValidation.Alphanumeric:
                    return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
                default:
                    return true;
            }
        }

        private static void ForEachLabel(GameObject[] targets, System.Action<TMP_Text> apply)
        {
            if (targets == null) return;
            foreach (var go in targets)
            {
                var label = go != null ? go.GetComponentInChildren<TMP_Text>(true) : null;
                if (label != null) apply(label);
            }
        }

        private static void ForEachKeyColor(GameObject[] targets, Color32 color)
        {
            if (targets == null) return;
            foreach (var go in targets)
            {
                if (go == null) continue;

                var key = go.GetComponent<RewiredOnScreenKeyboardKey>();
                if (key != null)
                {
                    key.SetBaseColor(color);
                    continue;
                }

                var image = go.GetComponent<Image>();
                if (image != null) image.color = color;
            }
        }

        private static void ForEachImage(GameObject[] targets, System.Action<Image> apply)
        {
            if (targets == null) return;
            foreach (var go in targets)
            {
                var image = go != null ? go.GetComponent<Image>() : null;
                if (image != null) apply(image);
            }
        }
    }
}
