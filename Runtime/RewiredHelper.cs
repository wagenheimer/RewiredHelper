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
    public static Wagenheimer.RewiredHelper.RewiredInputManager instance
    {
        get
        {
            if (Instance == null)
            {
#if UNITY_2023_1_OR_NEWER
                Instance = Object.FindAnyObjectByType<Wagenheimer.RewiredHelper.RewiredInputManager>();
#else
                Instance = Object.FindObjectOfType<Wagenheimer.RewiredHelper.RewiredInputManager>();
#endif
            }
            return Instance;
        }
    }
}
