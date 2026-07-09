using UnityEngine;
using Wagenheimer.RewiredHelper;

/// <summary>
/// Minimal bootstrap example for Rewired Helper.
///
/// Attach this (or a RewiredInputManager directly) to a persistent GameObject in your first
/// scene. This example uses none of the optional integration interfaces — the manager runs
/// with harmless defaults (input is never considered blocked, no modal stack, controller
/// help is always allowed to show).
/// </summary>
[RequireComponent(typeof(RewiredInputManager))]
public class RewiredHelperBootstrap : MonoBehaviour
{
    private void Awake()
    {
        var manager = GetComponent<RewiredInputManager>();

        // Pass your own IUiBlocker / IModalStackProvider / IControllerHelpGate implementations
        // here if your game needs to suppress Escape/Return routing or gate controller help.
        manager.Configure();
    }
}
