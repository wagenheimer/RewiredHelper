using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Lets the host game's modal/dialog stack take priority over the generic
    /// <see cref="EscapeButton"/>/<see cref="ReturnEscapeEvent"/> routing — e.g. if a modal
    /// is open, Escape/Return should trigger its buttons instead. Optional — if none is
    /// supplied to <see cref="RewiredInputManager.Initialize"/>, the stack is treated as empty
    /// and Escape/Return fall straight through to the generic routing.
    /// </summary>
    public interface IModalStackProvider
    {
        int ModalCount { get; }

        /// <summary>Removes the top modal from the stack if it's null or inactive.</summary>
        void PruneInactiveTop();

        bool TryGetTopEscapeButton(out Button escapeButton);

        bool TryGetTopOkButton(out Button okButton);
    }

    internal sealed class NullModalStackProvider : IModalStackProvider
    {
        public static readonly NullModalStackProvider Instance = new();

        public int ModalCount => 0;

        public void PruneInactiveTop() { }

        public bool TryGetTopEscapeButton(out Button escapeButton)
        {
            escapeButton = null;
            return false;
        }

        public bool TryGetTopOkButton(out Button okButton)
        {
            okButton = null;
            return false;
        }
    }
}
