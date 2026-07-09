using System;
using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace Wagenheimer.RewiredHelper.UI
{
    /// <summary>
    /// Lists every action currently mapped on the player's last active controller: one
    /// <see cref="ControllerHelpRow"/> per binding, with an icon when Rewired has a glyph for it
    /// and a text fallback (element name — action name) otherwise.
    ///
    /// Use <b>Tools → Wagenheimer → Rewired Helper → Create Default Controller Help UI</b> to
    /// generate a bare-bones panel wired to this component in the current scene, then restyle it
    /// (art, layout) and save it as a prefab in your own project. Wire
    /// <c>RewiredInputManager.OnShowControllerHelp</c> to activate the panel and call
    /// <see cref="Populate"/>. Localization is entirely up to you — see
    /// <see cref="ActionNameLocalizer"/> to route action names through I2 or any other system.
    /// </summary>
    public class ControllerHelpPanel : MonoBehaviour
    {
        [Tooltip("Rewired player id to read mapped actions from.")]
        public int PlayerId = 0;

        [Tooltip("Row template, deactivated in the hierarchy. Cloned once per mapped action found.")]
        public ControllerHelpRow RowTemplate;

        [Tooltip("Parent transform rows are instantiated under (e.g. inside a Vertical Layout Group in a ScrollRect).")]
        public Transform RowContainer;

        /// <summary>
        /// Optional hook to localize an action's display name (e.g. via I2 Localization's
        /// LocalizationManager.GetTranslation). If null, falls back to Rewired's own localized
        /// <see cref="ActionElementMap.actionDescriptiveName"/>.
        /// </summary>
        public Func<InputAction, string> ActionNameLocalizer;

        readonly List<ControllerHelpRow> _spawnedRows = new List<ControllerHelpRow>();
        readonly List<ControllerMap> _maps = new List<ControllerMap>();

        /// <summary>Destroys existing rows and rebuilds them from the current controller bindings.</summary>
        public void Populate()
        {
            Clear();

            if (RowTemplate == null || RowContainer == null)
            {
                Debug.LogWarning("[ControllerHelpPanel] RowTemplate/RowContainer not assigned.");
                return;
            }

            var player = ReInput.players.GetPlayer(PlayerId);
            var controller = player?.controllers.GetLastActiveController();
            if (controller == null)
                return;

            _maps.Clear();
            player.controllers.maps.GetAllMaps(controller.controllerType, _maps);

            foreach (var map in _maps)
            {
                foreach (var elementMap in map.ElementMaps)
                {
                    if (elementMap == null)
                        continue;

                    var row = Instantiate(RowTemplate, RowContainer);
                    row.gameObject.SetActive(true);
                    row.SetLabel(BuildLabel(elementMap));
                    row.SetGlyph(elementMap.elementIdentifierGlyph);
                    _spawnedRows.Add(row);
                }
            }
        }

        string BuildLabel(ActionElementMap elementMap)
        {
            string actionName = elementMap.actionDescriptiveName;

            if (ActionNameLocalizer != null)
            {
                var action = ReInput.mapping.GetAction(elementMap.actionId);
                if (action != null)
                    actionName = ActionNameLocalizer(action);
            }

            return $"{elementMap.elementIdentifierName} — {actionName}";
        }

        /// <summary>Destroys all spawned rows, leaving the template untouched.</summary>
        public void Clear()
        {
            for (int i = 0; i < _spawnedRows.Count; i++)
                if (_spawnedRows[i] != null)
                    Destroy(_spawnedRows[i].gameObject);

            _spawnedRows.Clear();
        }
    }
}
