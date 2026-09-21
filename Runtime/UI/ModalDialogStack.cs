using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Wagenheimer.RewiredHelper;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Generic modal dialog stack: tracks which <see cref="Dialog"/>s are currently open
    /// and drives their show/hide animation. Pair with <see cref="DefaultModalStackProvider"/>
    /// to plug this into <see cref="RewiredInputManager.Configure"/>'s <c>modalStack</c> hook so
    /// Escape/Return prioritize the top modal's <see cref="Dialog.EscapeButton"/>/<see cref="Dialog.OkButton"/>.
    /// </summary>
    public static class ModalDialogStack
    {
        public static List<Dialog> Modals { get; } = new();

        /// <summary>
        /// Optional host hook. When it returns true, <see cref="IsThereAnyVisible"/> reports false
        /// even with modals open (e.g. a face/tutorial dialog the host treats as "not blocking the game").
        /// </summary>
        public static Func<bool> SuppressVisibility;

        /// <summary>
        /// Optional host hook used by <see cref="CloseModals"/>. Return true when the host handled the
        /// close itself (custom animation, audio, etc.) so the default <c>SetActive(false)</c> is skipped.
        /// </summary>
        public static Func<Dialog, bool> CloseModalOverride;

        /// <summary>Raised whenever a dialog is shown, carrying the show delay. Host can hook audio/sfx.</summary>
        public static event Action<Dialog, float> DialogShown;

        /// <summary>Raised whenever a dialog starts to close. Host can hook audio/sfx.</summary>
        public static event Action<Dialog> DialogClosed;

        public static bool IsThereAnyVisible => Modals.Count > 0 && !(SuppressVisibility?.Invoke() ?? false);

        public static void ShowDialog(GameObject dialog, bool mostraMesmoSeEstiverNaListaBloqueio = false) =>
            ShowDialog(dialog.GetComponent<Dialog>());

        public static void ShowDialog(RectTransform dialog, bool mostraMesmoSeEstiverNaListaBloqueio = false) =>
            ShowDialog(dialog.GetComponent<Dialog>());

        public static void ShowDialog(Dialog dialog, float delay = 0f,
            ShowDialogEffect effect = ShowDialogEffect.Fade, Action onShow = null, bool mostraMesmoSeEstiverNaListaBloqueio = false)
        {
            if (dialog == null) return;
            if (Modals.Contains(dialog)) return; // already open

            // Already visible or mid-animation (showing/hiding): abort instead of restarting
            // tweens on top of each other, which used to cause flicker and stuck overlays.
            if (dialog.IsPlayingShow || dialog.IsPlayingHide) return;

            Modals.Add(dialog);
            dialog.ShowEffect = effect;
            dialog.RequestBlockUi(0.7f + delay);
            DialogShown?.Invoke(dialog, delay);
            dialog.PlayShow(delay, onShow);
        }

        public static void CloseDialog(GameObject dialog, Action onHide = null) =>
            CloseDialog(dialog.GetComponent<Dialog>(), onHide);

        public static void CloseDialog(Dialog dialog, Action onHide = null)
        {
            if (dialog == null || !dialog.gameObject.activeSelf) return;

            Modals.Remove(dialog);
            dialog.RequestBlockUi(dialog.ShowHideDialogTime + dialog.FadeBlackTime);
            DialogClosed?.Invoke(dialog);
            dialog.PlayHide(() =>
            {
                dialog.AfterHide?.Invoke();
                onHide?.Invoke();
                dialog.OnHide?.Invoke();
                dialog.gameObject.SetActive(false);
            });
        }

        /// <summary>Immediately hides every open modal without animation (e.g. on scene transition).</summary>
        public static void CloseModals()
        {
            foreach (var dialog in Modals.Where(d => d != null).ToList())
            {
                if (CloseModalOverride != null && CloseModalOverride(dialog)) continue;
                dialog.gameObject.SetActive(false);
            }

            Modals.Clear();
        }
    }

    /// <summary>
    /// Legacy alias kept for compatibility with the original game API (<c>Dialogs.ShowDialog</c>).
    /// Forwards everything to <see cref="ModalDialogStack"/>.
    /// </summary>
    public static class Dialogs
    {
        public static List<Dialog> Modals => ModalDialogStack.Modals;

        /// <summary>Legacy list kept so markers can register themselves as "do not show dialogs now".</summary>
        public static List<IBloqueiaDialogosDeSerExibidos> ListaBloqueiaDialogosDeSerExibidos { get; } = new();

        public static bool IsThereAnyVisible => ModalDialogStack.IsThereAnyVisible;

        public static void ShowDialog(GameObject dialog, bool mostraMesmoSeEstiverNaListaBloqueio = false) =>
            ModalDialogStack.ShowDialog(dialog, mostraMesmoSeEstiverNaListaBloqueio);

        public static void ShowDialog(RectTransform dialog, bool mostraMesmoSeEstiverNaListaBloqueio = false) =>
            ModalDialogStack.ShowDialog(dialog, mostraMesmoSeEstiverNaListaBloqueio);

        public static void ShowDialog(Dialog dialog, float delay = 0f,
            ShowDialogEffect effect = ShowDialogEffect.Fade, Action onShow = null, bool mostraMesmoSeEstiverNaListaBloqueio = false) =>
            ModalDialogStack.ShowDialog(dialog, delay, effect, onShow, mostraMesmoSeEstiverNaListaBloqueio);

        public static void CloseDialog(GameObject dialog, Action onHide = null) =>
            ModalDialogStack.CloseDialog(dialog, onHide);

        public static void CloseDialog(Dialog dialog, Action onHide = null) =>
            ModalDialogStack.CloseDialog(dialog, onHide);

        public static void CloseModals() => ModalDialogStack.CloseModals();
    }

    /// <summary>
    /// Ready-to-use <see cref="IModalStackProvider"/> backed by <see cref="ModalDialogStack"/>.
    /// Pass an instance to <c>RewiredInputManager.Configure(modalStack: new DefaultModalStackProvider())</c>.
    /// </summary>
    public sealed class DefaultModalStackProvider : IModalStackProvider
    {
        public int ModalCount => ModalDialogStack.Modals.Count;

        public void PruneInactiveTop()
        {
            while (ModalDialogStack.Modals.Count > 0 &&
                   (ModalDialogStack.Modals[ModalDialogStack.Modals.Count - 1] == null ||
                    !ModalDialogStack.Modals[ModalDialogStack.Modals.Count - 1].gameObject.activeInHierarchy))
            {
                ModalDialogStack.Modals.RemoveAt(ModalDialogStack.Modals.Count - 1);
            }
        }

        public bool TryGetTopEscapeButton(out UnityEngine.UI.Button escapeButton)
        {
            escapeButton = ModalDialogStack.Modals.Count > 0 ? ModalDialogStack.Modals[ModalDialogStack.Modals.Count - 1]?.EscapeButton : null;
            return escapeButton != null;
        }

        public bool TryGetTopOkButton(out UnityEngine.UI.Button okButton)
        {
            okButton = ModalDialogStack.Modals.Count > 0 ? ModalDialogStack.Modals[ModalDialogStack.Modals.Count - 1]?.OkButton : null;
            return okButton != null;
        }
    }
}
