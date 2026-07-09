using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using Wagenheimer.RewiredHelper;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Generic modal dialog stack: tracks which <see cref="ModalDialog"/>s are currently open
    /// and drives their show/hide animation. Pair with <see cref="DefaultModalStackProvider"/>
    /// to plug this into <see cref="RewiredInputManager.Configure"/>'s <c>modalStack</c> hook so
    /// Escape/Return prioritize the top modal's <see cref="ModalDialog.EscapeButton"/>/<see cref="ModalDialog.OkButton"/>.
    /// </summary>
    public static class ModalDialogStack
    {
        public static List<ModalDialog> Modals { get; } = new();

        public static bool IsThereAnyVisible => Modals.Count > 0;

        public static void ShowDialog(GameObject dialog) => ShowDialog(dialog.GetComponent<ModalDialog>());

        public static void ShowDialog(ModalDialog dialog, float delay = 0f,
            ShowDialogEffect effect = ShowDialogEffect.Fade, Action onShow = null)
        {
            if (Modals.Contains(dialog))
                return; // already open — bring-to-front is left to the host if it needs sibling reordering

            Modals.Add(dialog);
            dialog.ShowEffect = effect;
            dialog.RequestBlockUi(0.7f + delay);
            dialog.PlayShow(delay, onShow);
        }

        public static void CloseDialog(GameObject dialog, Action onHide = null) =>
            CloseDialog(dialog.GetComponent<ModalDialog>(), onHide);

        public static void CloseDialog(ModalDialog dialog, Action onHide = null)
        {
            if (!dialog.gameObject.activeSelf) return;

            Modals.Remove(dialog);
            dialog.RequestBlockUi(dialog.ShowHideDialogTime + dialog.FadeBlackTime);
            dialog.PlayHide(() =>
            {
                dialog.AfterHide?.Invoke();
                onHide?.Invoke();
                dialog.OnHide?.Invoke();
                dialog.gameObject.SetActive(false);
            });
            dialog.AfterShow?.Invoke();
        }

        /// <summary>Immediately hides every open modal without animation (e.g. on scene transition).</summary>
        public static void CloseModals()
        {
            foreach (var dialog in Modals.Where(d => d != null).ToList())
                dialog.gameObject.SetActive(false);

            Modals.Clear();
        }
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
            if (ModalDialogStack.Modals.Count > 0 &&
                (ModalDialogStack.Modals[0] == null || !ModalDialogStack.Modals[0].gameObject.activeSelf))
            {
                ModalDialogStack.Modals.RemoveAt(0);
            }
        }

        public bool TryGetTopEscapeButton(out UnityEngine.UI.Button escapeButton)
        {
            escapeButton = ModalDialogStack.Modals.Count > 0 ? ModalDialogStack.Modals[0]?.EscapeButton : null;
            return escapeButton != null;
        }

        public bool TryGetTopOkButton(out UnityEngine.UI.Button okButton)
        {
            okButton = ModalDialogStack.Modals.Count > 0 ? ModalDialogStack.Modals[0]?.OkButton : null;
            return okButton != null;
        }
    }
}
