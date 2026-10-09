using NUnit.Framework;

using TMPro;

using UnityEngine;
using UnityEngine.EventSystems;

using Wagenheimer.RewiredHelper.UI;

namespace Wagenheimer.RewiredHelper.Tests
{
    /// <summary>
    /// The on-screen keyboard's own logic (independent of how it is skinned/built): typing, backspace, the
    /// character limit, case toggling, and the Setup Audit / RewiredInputManager wiring around it.
    /// </summary>
    public class RewiredOnScreenKeyboardTests
    {
        private GameObject _keyboardGo;
        private RewiredOnScreenKeyboard _keyboard;
        private GameObject _fieldGo;
        private TMP_InputField _field;
        private GameObject _eventSystemGo;

        [SetUp]
        public void SetUp()
        {
            _eventSystemGo = new GameObject("EventSystem", typeof(EventSystem));

            _keyboardGo = new GameObject("Keyboard", typeof(RectTransform));
            _keyboard = _keyboardGo.AddComponent<RewiredOnScreenKeyboard>();

            _fieldGo = new GameObject("Field", typeof(RectTransform));
            _field = _fieldGo.AddComponent<TMP_InputField>();
            _field.characterLimit = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_keyboardGo);
            Object.DestroyImmediate(_fieldGo);
            Object.DestroyImmediate(_eventSystemGo);
        }

        private TMP_Text CreateLabel(string text)
        {
            var go = new GameObject("Label");
            var label = go.AddComponent<TextMeshProUGUI>();
            label.text = text;
            return label;
        }

        [Test]
        public void WriteKey_AppendsTheLabelTextToTheFocusedField()
        {
            _keyboard.SetFocus(_field);
            _field.text = "ab";

            _keyboard.WriteKey(CreateLabel("c"));

            Assert.AreEqual("abc", _field.text);
        }

        [Test]
        public void WriteKey_WithNoFocus_DoesNothing()
        {
            Assert.DoesNotThrow(() => _keyboard.WriteKey(CreateLabel("x")));
        }

        [Test]
        public void WriteKey_RespectsTheCharacterLimit()
        {
            _keyboard.SetFocus(_field);
            _field.characterLimit = 3;
            _field.text = "ab";

            _keyboard.WriteKey(CreateLabel("cd")); // would push it to 4 chars

            Assert.AreEqual("abc", _field.text);
        }

        [Test]
        public void Backspace_RemovesTheLastCharacter()
        {
            _keyboard.SetFocus(_field);
            _field.text = "abc";

            _keyboard.WriteSpecialKey(0);

            Assert.AreEqual("ab", _field.text);
        }

        [Test]
        public void Backspace_OnEmptyField_DoesNothing()
        {
            _keyboard.SetFocus(_field);
            _field.text = "";

            Assert.DoesNotThrow(() => _keyboard.WriteSpecialKey(0));
            Assert.AreEqual("", _field.text);
        }

        [Test]
        public void SetActiveFocus_ShowsTheKeyboardAndMovesTheCaretToTheEnd()
        {
            _keyboard.SetActiveFocus(_field);

            Assert.IsTrue(_keyboard.isActive);
            Assert.AreSame(_field, _keyboard.focus);
        }

        [Test]
        public void SetActive_False_TogglesActiveOffWithNoAnimator()
        {
            _keyboard.SetActiveFocus(_field);

            _keyboard.SetActive(false);

            Assert.IsFalse(_keyboard.isActive);
        }

        [Test]
        public void SwitchCaps_UppercasesConfiguredKeyLabels()
        {
            var keyGo = new GameObject("Key_a");
            keyGo.transform.SetParent(_keyboardGo.transform);
            var keyLabel = keyGo.AddComponent<TextMeshProUGUI>();
            keyLabel.text = "a";
            _keyboard.keys = new[] { keyGo };

            _keyboard.SwitchCaps();

            Assert.AreEqual("A", keyLabel.text);
            Assert.IsTrue(_keyboard.capsEnabled);

            _keyboard.SwitchCaps();
            Assert.AreEqual("a", keyLabel.text);
        }

        [Test]
        public void OnKeyPressed_FiresOnEveryKeyPress()
        {
            int presses = 0;
            _keyboard.OnKeyPressed.AddListener(() => presses++);
            _keyboard.SetFocus(_field);

            _keyboard.WriteKey(CreateLabel("a"));
            _keyboard.WriteSpecialKey(0);

            Assert.AreEqual(2, presses);
        }

        [Test]
        public void WriteKey_KeepsOnlyDigits_WhenTheFieldIsDigitOnly()
        {
            _keyboard.SetFocus(_field);
            _field.characterValidation = TMP_InputField.CharacterValidation.Digit;
            _field.text = string.Empty;

            _keyboard.WriteKey(CreateLabel("a7b"));

            Assert.AreEqual("7", _field.text);
        }

        [Test]
        public void WriteKey_DoesNothing_WhenTheFieldIsReadOnly()
        {
            _keyboard.SetFocus(_field);
            _field.text = "ab";
            _field.readOnly = true;

            _keyboard.WriteKey(CreateLabel("c"));

            Assert.AreEqual("ab", _field.text);
        }

        [Test]
        public void Close_HidesTheKeyboardAndRemembersTheField()
        {
            _keyboard.SetActiveFocus(_field);

            _keyboard.Close();

            Assert.IsFalse(_keyboard.isActive);
            Assert.AreSame(_field, _keyboard.DismissedFor);
        }

        [Test]
        public void ClearDismissed_ForAnotherSelection_AllowsTheKeyboardToReopen()
        {
            _keyboard.SetActiveFocus(_field);
            _keyboard.Close();

            _keyboard.ClearDismissed(_field);
            Assert.AreSame(_field, _keyboard.DismissedFor, "Still on the same field: must stay dismissed.");

            _keyboard.ClearDismissed(null);
            Assert.IsNull(_keyboard.DismissedFor);
        }

        [Test]
        public void CapsKeyPressed_CyclesOffShiftLockOff()
        {
            _keyboard.CapsKeyPressed();
            Assert.IsTrue(_keyboard.capsEnabled);
            Assert.IsFalse(_keyboard.CapsLocked);

            _keyboard.CapsKeyPressed();
            Assert.IsTrue(_keyboard.capsEnabled);
            Assert.IsTrue(_keyboard.CapsLocked);

            _keyboard.CapsKeyPressed();
            Assert.IsFalse(_keyboard.capsEnabled);
            Assert.IsFalse(_keyboard.CapsLocked);
        }

        [Test]
        public void CapsKeyPressed_TurnsOffAnAutoShift_WithASinglePress()
        {
            _field.text = string.Empty;
            _keyboard.SetActiveFocus(_field);
            Assert.IsTrue(_keyboard.capsEnabled, "Auto-capitalisation starts the empty field in Shift.");

            _keyboard.CapsKeyPressed();

            Assert.IsFalse(_keyboard.capsEnabled);
            Assert.IsFalse(_keyboard.CapsLocked);
        }

        [Test]
        public void WriteKey_SpendsAOneLetterShift()
        {
            _keyboard.autoCapitalize = false;
            _keyboard.SetFocus(_field);
            _field.text = string.Empty;
            _keyboard.SetCaps(true);

            _keyboard.WriteKey(CreateLabel("A"));

            Assert.AreEqual("A", _field.text);
            Assert.IsFalse(_keyboard.capsEnabled);
        }

        [Test]
        public void WriteKey_KeepsCapsLockOn()
        {
            _keyboard.autoCapitalize = false;
            _keyboard.SetFocus(_field);
            _field.text = string.Empty;
            _keyboard.CapsKeyPressed();
            _keyboard.CapsKeyPressed();

            _keyboard.WriteKey(CreateLabel("A"));

            Assert.IsTrue(_keyboard.capsEnabled);
            Assert.IsTrue(_keyboard.CapsLocked);
        }

        [Test]
        public void SetActiveFocus_TurnsShiftOnForAnEmptyField_WhenAutoCapitalizing()
        {
            _field.text = string.Empty;

            _keyboard.SetActiveFocus(_field);

            Assert.IsTrue(_keyboard.capsEnabled);
        }

        [Test]
        public void Key_IgnoresASecondPressInTheSameMoment()
        {
            _keyboard.autoCapitalize = false;
            _keyboard.SetFocus(_field);
            _field.text = string.Empty;

            var keyGo = new GameObject("Key_a", typeof(RectTransform), typeof(UnityEngine.UI.Button), typeof(RewiredOnScreenKeyboardKey));
            var key = keyGo.GetComponent<RewiredOnScreenKeyboardKey>();
            key.keyboard = _keyboard;
            key.label = CreateLabel("a");

            key.Press();
            key.Press();

            Assert.AreEqual("a", _field.text);
            Object.DestroyImmediate(keyGo);
        }

        [Test]
        public void ReopenFor_ShowsTheKeyboardAgainAfterItWasClosed()
        {
            _keyboard.SetActiveFocus(_field);
            _keyboard.Close();
            Assert.IsFalse(_keyboard.isActive);

            _keyboard.ReopenFor(_field);

            Assert.IsTrue(_keyboard.isActive);
            Assert.IsNull(_keyboard.DismissedFor);
        }

        [Test]
        public void ContainsSelection_IsTrueOnlyForTheKeyboardAndItsChildren()
        {
            var key = new GameObject("Key", typeof(RectTransform));
            key.transform.SetParent(_keyboardGo.transform);

            Assert.IsTrue(_keyboard.ContainsSelection(key));
            Assert.IsTrue(_keyboard.ContainsSelection(_keyboardGo));
            Assert.IsFalse(_keyboard.ContainsSelection(_fieldGo));
            Assert.IsFalse(_keyboard.ContainsSelection(null));
        }
    }

    public class RewiredOnScreenKeyboardWiringTests
    {
        private GameObject _managerGo;
        private RewiredInputManager _manager;
        private GameObject _keyboardGo;
        private RewiredOnScreenKeyboard _keyboard;

        [SetUp]
        public void SetUp()
        {
            _managerGo = new GameObject("Manager");
            _manager = _managerGo.AddComponent<RewiredInputManager>();

            _keyboardGo = new GameObject("Keyboard");
            _keyboard = _keyboardGo.AddComponent<RewiredOnScreenKeyboard>();
        }

        [TearDown]
        public void TearDown()
        {
            RewiredInputManager.UnregisterOnScreenKeyboard(_keyboard);
            Object.DestroyImmediate(_managerGo);
            Object.DestroyImmediate(_keyboardGo);
        }

        [Test]
        public void RegisterAndUnregister_ControlWhetherItCountsAsActive()
        {
            _keyboard.SetActiveFocus(null);
            RewiredInputManager.RegisterOnScreenKeyboard(_keyboard);
            Assert.IsTrue(RewiredInputManager.IsOnScreenKeyboardActive);

            RewiredInputManager.UnregisterOnScreenKeyboard(_keyboard);
            Assert.IsFalse(RewiredInputManager.IsOnScreenKeyboardActive);
        }

        [Test]
        public void ManagerDefaultsToShowingTheKeyboardOnGamepadTextInput()
        {
            Assert.IsTrue(_manager.ShowOnScreenKeyboardOnGamepadTextInput);
        }

        [Test]
        public void ManagerLiftsFormsAboveTheKeyboardByDefault()
        {
            Assert.IsTrue(_manager.AutoLiftFormsAboveKeyboard);
        }

        [Test]
        public void FindFormRoot_FallsBackToTheTopLevelPanelUnderTheCanvas()
        {
            var canvasGo = new GameObject("Canvas", typeof(Canvas));
            var panel = new GameObject("Panel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            var inner = new GameObject("Inner", typeof(RectTransform));
            inner.transform.SetParent(panel.transform, false);
            var field = new GameObject("Field", typeof(RectTransform));
            field.transform.SetParent(inner.transform, false);

            var root = RewiredInputManager.FindFormRoot(field.transform);

            Assert.AreSame(panel.transform, root);
            Object.DestroyImmediate(canvasGo);
        }

        [Test]
        public void ManagerHidesTheJoystickCursorWhileTheKeyboardIsOpenByDefault()
        {
            Assert.IsTrue(_manager.HideCursorWhileOnScreenKeyboardOpen);
        }

        [Test]
        public void ManagerDefaultsToTheSteamDeckOnly()
        {
            Assert.IsTrue(_manager.OnScreenKeyboardOnlyOnSteamDeck);
        }

        [TestCase(true, false, false, false)]  // Steam Deck only, not a Deck, no simulation: hidden
        [TestCase(true, true, false, true)]    // on a Steam Deck: shown
        [TestCase(true, false, true, true)]    // testing in the Editor: shown
        [TestCase(false, false, false, true)]  // restriction turned off: any gamepad
        public void IsOnScreenKeyboardAllowed_FollowsTheSteamDeckRule(bool onlyOnDeck, bool isDeck, bool simulated, bool expected)
        {
            Assert.AreEqual(expected, RewiredInputManager.IsOnScreenKeyboardAllowed(onlyOnDeck, isDeck, simulated));
        }
    }
}
