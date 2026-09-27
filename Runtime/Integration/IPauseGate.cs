namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Lets the host game veto automatic pauses (e.g. while an ad, an IAP sheet or a loading screen
    /// is active, where the OS focus loss is expected). Optional — if none is supplied to
    /// <see cref="RewiredInputManager.Configure"/>, every pause is allowed.
    /// Manual pauses (<see cref="PauseReason.Manual"/>) are never vetoed.
    /// </summary>
    public interface IPauseGate
    {
        bool CanPause(PauseReason reason);
    }

    internal sealed class NullPauseGate : IPauseGate
    {
        public static readonly NullPauseGate Instance = new();
        public bool CanPause(PauseReason reason) => true;
    }
}
