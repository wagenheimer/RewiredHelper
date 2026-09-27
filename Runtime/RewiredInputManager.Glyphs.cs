using System;
using System.Reflection;

using Rewired;

using UnityEngine;

namespace Wagenheimer.RewiredHelper
{
    public partial class RewiredInputManager
    {
        #region Glyph Selector Configuration
        /// <summary>
        /// Places Custom Controllers last in the Rewired glyph selector's controller type order.
        /// The Rewired default order is [Joystick, Custom, Mouse, Keyboard], which means binds on
        /// auxiliary/orphaned Custom Controllers (e.g. AndroidRemote) take precedence over mouse/keyboard
        /// glyphs on desktop, causing raw element names to be displayed instead of glyphs.
        /// Applied via reflection because the Rewired glyph system scripts live in Assembly-CSharp,
        /// which this package's assembly cannot reference directly.
        /// </summary>
        private void ConfigureGlyphControllerTypeOrder()
        {
            if (!ReInput.isReady) return;
            _glyphSelectorConfigured = true;

            if (!ForceGlyphCustomControllerLast) return;

            try
            {
                var optionsType = FindType("Rewired.Glyphs.ControllerElementGlyphSelectorOptions");
                if (optionsType == null) return; // Rewired glyph system not installed in this project

                var orderProperty = optionsType.GetProperty("controllerTypeOrder");
                if (orderProperty == null || !orderProperty.CanWrite)
                {
                    Debug.LogWarning("[RewiredHelper] Could not find a writable controllerTypeOrder property on ControllerElementGlyphSelectorOptions.");
                    return;
                }

                var newOrder = new[]
                {
                    ControllerType.Joystick,
                    ControllerType.Mouse,
                    ControllerType.Keyboard,
                    ControllerType.Custom
                };

                // 1. Patch the global default options (used by glyph helpers with no options asset assigned)
                var defaultOptionsProperty = optionsType.GetProperty("defaultOptions", BindingFlags.Public | BindingFlags.Static);
                var defaultOptions = defaultOptionsProperty?.GetValue(null);
                if (defaultOptions != null)
                {
                    orderProperty.SetValue(defaultOptions, newOrder);
                }

                // 2. Patch glyph helper components that use an assigned options asset,
                //    since those serialize their own copy of the controller type order.
                PatchGlyphHelperGlyphOptions("Rewired.Glyphs.UnityUI.UnityUITextMeshProGlyphHelper", orderProperty, newOrder);
                PatchGlyphHelperGlyphOptions("Rewired.Glyphs.UnityUI.UnityUIPlayerControllerElementGlyph", orderProperty, newOrder);
                PatchGlyphHelperGlyphOptions("Rewired.Glyphs.UnityUI.UnityUIControllerElementGlyph", orderProperty, newOrder);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RewiredHelper] Failed to configure glyph controller type order: " + ex.Message);
            }
        }

        private static void PatchGlyphHelperGlyphOptions(string helperTypeName, PropertyInfo orderProperty, ControllerType[] newOrder)
        {
            var helperType = FindType(helperTypeName);
            if (helperType == null) return;

            var optionsProperty = helperType.GetProperty("options");
            if (optionsProperty == null || !optionsProperty.CanRead) return;

#pragma warning disable 0618
            var components = UnityEngine.Object.FindObjectsOfType(helperType, true);
#pragma warning restore 0618

            foreach (var component in components)
            {
                try
                {
                    // The helper's "options" property returns the ScriptableObject wrapper
                    // (ControllerElementGlyphSelectorOptionsSOBase); the actual options live
                    // in its own "options" property.
                    var wrapper = optionsProperty.GetValue(component);
                    if (wrapper == null) continue;

                    var wrapperType = wrapper.GetType();
                    var innerOptionsProperty = wrapperType.GetProperty("options");
                    if (innerOptionsProperty == null || !innerOptionsProperty.CanRead) continue;

                    var innerOptions = innerOptionsProperty.GetValue(wrapper);
                    if (innerOptions == null) continue;

                    orderProperty.SetValue(innerOptions, newOrder);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RewiredHelper] Failed to patch glyph options on '{helperTypeName}': {ex.Message}");
                }
            }
        }

        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type;
                try { type = assembly.GetType(fullName, false); }
                catch { continue; }
                if (type != null) return type;
            }
            return null;
        }
        #endregion
    }
}
