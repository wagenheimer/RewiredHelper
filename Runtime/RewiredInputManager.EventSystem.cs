using System.Linq;

using UnityEngine;
using UnityEngine.EventSystems;

namespace Wagenheimer.RewiredHelper
{
    /// <summary>
    /// Keeps Rewired's Event System the one Unity actually uses. UI Toolkit creates a default Event System (plain
    /// <c>StandaloneInputModule</c>) as soon as a runtime panel exists and none does yet. The debug overlays of the other
    /// Wagenheimer packages (IAP, Rate, Social, Build...) auto-start in the Editor and development builds, before the scene's
    /// Rewired Event System is created. Unity then logs "There can be only one active Event System" and keeps the
    /// <em>first</em> one as <c>EventSystem.current</c>, so Rewired's module never runs and gamepad UI input is dead.
    /// </summary>
    public partial class RewiredInputManager
    {
        private const string RewiredModuleTypeName = "RewiredStandaloneInputModule";
        private const float EventSystemCheckInterval = 1f;

        private float _nextEventSystemCheck;
        private bool _loggedForeignEventSystem;

        private void ResolveForeignEventSystemsThrottled()
        {
            if (Time.unscaledTime < _nextEventSystemCheck) return;

            _nextEventSystemCheck = Time.unscaledTime + EventSystemCheckInterval;
            ResolveForeignEventSystems();
        }

        /// <summary>
        /// When a Rewired Event System exists, removes any other Event System that does not run Rewired's input module.
        /// Does nothing when there is only one, or when none of them uses Rewired (then the project has no Rewired UI to protect).
        /// </summary>
        internal void ResolveForeignEventSystems()
        {
            var all = FindAllEventSystems();
            if (all.Length < 2) return;

            if (!all.Any(HasRewiredModule)) return;

            foreach (var eventSystem in all)
            {
                if (eventSystem == null || HasRewiredModule(eventSystem)) continue;

                RemoveEventSystem(eventSystem);
                if (_loggedForeignEventSystem) continue;

                _loggedForeignEventSystem = true;
                Debug.LogWarning("[RewiredHelper] Removed an Event System that does not use Rewired's input module (usually auto-created by a " +
                                 "UI Toolkit debug overlay before the scene's own Event System existed). Without this, gamepad UI input would be ignored.");
            }
        }

        private static EventSystem[] FindAllEventSystems()
        {
#if UNITY_2023_1_OR_NEWER
            return Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
#pragma warning disable 0618
            return Object.FindObjectsOfType<EventSystem>();
#pragma warning restore 0618
#endif
        }

        private static bool HasRewiredModule(EventSystem eventSystem) =>
            eventSystem != null && eventSystem.GetComponent(RewiredModuleTypeName) != null;

        /// <summary>Destroys the whole object when it only exists for the Event System; otherwise just its Event System and input modules.</summary>
        private static void RemoveEventSystem(EventSystem eventSystem)
        {
            var go = eventSystem.gameObject;
            var otherComponents = go.GetComponents<Component>()
                .Count(c => c != null && !(c is Transform) && !(c is EventSystem) && !(c is BaseInputModule));

            if (otherComponents == 0)
            {
                Object.Destroy(go);
                return;
            }

            foreach (var module in go.GetComponents<BaseInputModule>())
                Object.Destroy(module);
            Object.Destroy(eventSystem);
        }
    }
}
