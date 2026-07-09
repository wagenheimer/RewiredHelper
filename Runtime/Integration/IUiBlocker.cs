namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Lets the host game suppress Escape/Return routing while its own UI is in a blocked
    /// state (e.g. a cutscene or a system dialog). Optional — if none is supplied to
    /// <see cref="RewiredInputManager.Initialize"/>, input is never considered blocked.
    /// </summary>
    public interface IUiBlocker
    {
        bool IsUiBlocked { get; }
    }

    internal sealed class NullUiBlocker : IUiBlocker
    {
        public static readonly NullUiBlocker Instance = new();
        public bool IsUiBlocked => false;
    }
}
