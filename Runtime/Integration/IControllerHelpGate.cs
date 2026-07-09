namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Lets the host game decide when it's safe to show the first-time controller-help prompt
    /// (e.g. not while a loading screen is up, or before the main menu has been shown once).
    /// Optional — if none is supplied to <see cref="RewiredInputManager.Configure"/>, help is
    /// always considered safe to show.
    /// </summary>
    public interface IControllerHelpGate
    {
        bool CanShowControllerHelp { get; }
    }

    internal sealed class NullControllerHelpGate : IControllerHelpGate
    {
        public static readonly NullControllerHelpGate Instance = new();
        public bool CanShowControllerHelp => true;
    }
}
