using UnityEngine;

/// <summary>
/// Backward compatibility wrapper for projects using the legacy RewiredHelper name.
/// Inherits from <see cref="Wagenheimer.RewiredHelper.RewiredInputManager"/>.
/// </summary>
[AddComponentMenu("")]
public class RewiredHelper : Wagenheimer.RewiredHelper.RewiredInputManager
{
    /// <summary>
    /// Legacy singleton accessor matching the previous RewiredHelper.instance API.
    /// </summary>
    public static Wagenheimer.RewiredHelper.RewiredInputManager instance => Instance;
}
