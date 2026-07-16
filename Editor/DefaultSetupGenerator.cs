using System;
using System.Linq;
using System.Reflection;
using Rewired;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Scene-building helpers under <b>Tools → Wagenheimer → Rewired Helper</b> that create a
    /// standard <see cref="RewiredInputManager"/> setup and a controller-help form wired to it.
    /// These build plain GameObjects directly in the open scene (rather than shipping a
    /// hand-authored .prefab file, which this package's CI can't validate compiles or instantiates
    /// correctly) — save the result as a prefab in your own project once you're happy with it.
    ///
    /// The controller-help form's row list is populated at runtime by
    /// <see cref="Wagenheimer.RewiredHelper.UI.ControllerHelpRowBuilder"/> (attached to its
    /// "Content" container here), not baked at edit time — <c>ReInput.mapping.Actions</c> is only
    /// populated once Rewired is initialized, so edit-time generation would either see stale data
    /// or, before Play mode has ever run, nothing at all.
    /// </summary>
    internal static class DefaultSetupGenerator
    {
        const string InputManagerPrefabPath = "Packages/com.wagenheimer.rewiredhelper/Runtime/Prefabs/Rewired Input Manager.prefab";
        const string EventSystemPrefabPath = "Packages/com.wagenheimer.rewiredhelper/Runtime/Prefabs/Rewired Event System.prefab";
        const string FormControllerPrefabPath = "Packages/com.wagenheimer.rewiredhelper/Runtime/Prefabs/formController.prefab";

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Create Rewired Input Manager", priority = 11)]
        internal static void CreateRewiredInputManager()
        {
            // 1. Instantiate Rewired Input Manager if not present
            var rewiredManager = FindInputManagerInScene();
            if (rewiredManager == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InputManagerPrefabPath);
                if (prefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Create Rewired Input Manager");
                    rewiredManager = instance.GetComponent("InputManager");
                }
                else
                {
                    Debug.LogWarning($"[RewiredHelper] Prefab not found at {InputManagerPrefabPath}");
                }
            }

            // If still null (e.g. prefab missing), create a fallback empty gameobject
            GameObject managerGo = rewiredManager != null ? rewiredManager.gameObject : null;
            if (managerGo == null)
            {
                managerGo = new GameObject("Rewired Input Manager");
                Undo.RegisterCreatedObjectUndo(managerGo, "Create Rewired Input Manager");
            }

            // 2. Add our RewiredInputManager component if not present
            var helper = managerGo.GetComponent<RewiredInputManager>();
            if (helper == null)
            {
                helper = managerGo.AddComponent<RewiredInputManager>();
                Undo.RegisterCompleteObjectUndo(managerGo, "Add RewiredInputManager Component");
            }

            // 3. Instantiate Rewired Event System if not present
            var eventSystem = UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EventSystemPrefabPath);
                if (eventSystemPrefab != null)
                {
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(eventSystemPrefab);
                    Undo.RegisterCreatedObjectUndo(instance, "Create Rewired Event System");
                }
                else
                {
                    // Fallback to standard EventSystem
                    var go = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                        typeof(UnityEngine.EventSystems.StandaloneInputModule));
                    Undo.RegisterCreatedObjectUndo(go, "Create Event System");
                }
            }

            EnsureGlyphProvider(managerGo);

            Selection.activeGameObject = managerGo;
            MarkSceneDirty();
        }

        const string GlyphProviderTypeName = "Rewired.Glyphs.GlyphProvider";
        const string GlyphSetCollectionTypeName = "Rewired.Glyphs.GlyphSetCollection";

        internal static Type FindGlyphProviderType() => FindTypeByName(GlyphProviderTypeName);
        internal static Type FindGlyphSetCollectionType() => FindTypeByName(GlyphSetCollectionTypeName);

        private static Type FindTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }

        /// <summary>
        /// Adds Rewired's Glyph Provider (if the Glyphs addon is installed) and assigns a
        /// GlyphSetCollection asset found in the project, if any. Without a Glyph Provider,
        /// ReInput.glyphs.glyphProvider is never set and every glyph tag falls back to plain text —
        /// this is the single most common reason "the icons don't show up".
        /// </summary>
        internal static void EnsureGlyphProvider(GameObject managerGo)
        {
            var glyphProviderType = FindGlyphProviderType();
            if (glyphProviderType == null) return; // Glyphs addon not installed — nothing to do

            var existing = managerGo.GetComponent(glyphProviderType);
            bool created = existing == null;
            if (created)
            {
                existing = Undo.AddComponent(managerGo, glyphProviderType);
            }

            var so = new SerializedObject(existing);
            var collectionsProp = so.FindProperty("_glyphSetCollections");
            if (collectionsProp != null && collectionsProp.arraySize == 0)
            {
                var collectionType = FindGlyphSetCollectionType();
                if (collectionType != null)
                {
                    // Prefer the shortest matching asset path: per-controller sub-collections (e.g.
                    // "...(_Joysticks/_Templates/_KeyboardMouse).asset") are aggregated *inside* a
                    // root collection asset, so the root's name is naturally the shortest — assigning
                    // a sub-collection directly alongside the root would double-count its entries and
                    // log "duplicate glyph key" errors.
                    string bestPath = null;
                    foreach (var guid in AssetDatabase.FindAssets($"t:{collectionType.Name}"))
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (bestPath == null || path.Length < bestPath.Length)
                            bestPath = path;
                    }

                    if (bestPath != null)
                    {
                        var collectionAsset = AssetDatabase.LoadAssetAtPath(bestPath, collectionType);
                        collectionsProp.arraySize = 1;
                        collectionsProp.GetArrayElementAtIndex(0).objectReferenceValue = collectionAsset;
                        so.ApplyModifiedProperties();
                        Debug.Log($"[RewiredHelper] Assigned Glyph Set Collection '{bestPath}' to Glyph Provider.");
                    }
                    else
                    {
                        Debug.LogWarning("[RewiredHelper] Glyph Provider added, but no GlyphSetCollection asset was found. " +
                            "Install Rewired's Glyphs pack (Window > Rewired > Extras > Glyphs > Install) and assign a collection manually.");
                    }
                }
            }

            if (created)
            {
                Undo.RegisterCompleteObjectUndo(managerGo, "Add Glyph Provider");
                Debug.Log("[RewiredHelper] Added Glyph Provider to Rewired Input Manager so controller glyph icons can render.");
            }
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Create Controller Help Form", priority = 12)]
        internal static void CreateControllerHelpForm()
        {
            var canvas = FindOrCreateCanvas();
            var formGo = GenerateRowBasedHelpForm(canvas.transform);

            Undo.RegisterCreatedObjectUndo(formGo, "Create Controller Help Form");
            formGo.SetActive(false);

            Selection.activeGameObject = formGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created a custom Row-Based Controller Help form (inactive by default). " +
                "Wire RewiredInputManager.OnShowControllerHelp to SetActive(true) it.");
        }

        internal static void CreateControllerHelpFormAndWire(RewiredInputManager manager)
        {
            var canvas = FindOrCreateCanvas();
            var formGo = GenerateRowBasedHelpForm(canvas.transform);

            Undo.RegisterCreatedObjectUndo(formGo, "Create Controller Help Form");
            formGo.SetActive(false);

            if (manager != null)
            {
                // Register persistent listener to OnShowControllerHelp
                var methodInfo = typeof(GameObject).GetMethod("SetActive", new[] { typeof(bool) });
                if (methodInfo != null)
                {
                    var delegateAction = Delegate.CreateDelegate(typeof(UnityEngine.Events.UnityAction<bool>), formGo, methodInfo) as UnityEngine.Events.UnityAction<bool>;
                    UnityEditor.Events.UnityEventTools.AddBoolPersistentListener(manager.OnShowControllerHelp, delegateAction, true);
                }
            }

            Selection.activeGameObject = formGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created custom Row-Based Controller Help form and wired to OnShowControllerHelp event.");
        }

        internal static void CreatePauseScreenAndWire(RewiredInputManager manager, SerializedObject serializedObject)
        {
            var canvas = FindOrCreateCanvas();
            var pauseGo = CreatePauseScreen(canvas.transform);

            Undo.RegisterCreatedObjectUndo(pauseGo, "Create Pause Screen");

            if (serializedObject != null)
            {
                var prop = serializedObject.FindProperty("GamePaused");
                if (prop != null)
                {
                    prop.objectReferenceValue = pauseGo;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            Selection.activeGameObject = pauseGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created Pause Screen and linked to GamePaused field.");
        }

        internal static void CreateGameCursorAndWire(RewiredInputManager manager, SerializedObject serializedObject)
        {
            var canvas = FindOrCreateCanvas();
            
            // Create a GameCursor GameObject under the Canvas
            var cursorGo = new GameObject("GameCursor", typeof(RectTransform), typeof(Image));
            var cursorRect = (RectTransform)cursorGo.transform;
            cursorRect.SetParent(canvas.transform, false);
            cursorRect.anchorMin = new Vector2(0f, 1f); // top-left corner anchor
            cursorRect.anchorMax = new Vector2(0f, 1f);
            cursorRect.pivot = new Vector2(0f, 1f);      // top-left pivot
            cursorRect.sizeDelta = new Vector2(32, 32);
            cursorRect.anchoredPosition = Vector2.zero;

            var img = cursorGo.GetComponent<Image>();
            img.raycastTarget = false; // MUST be false so it doesn't block UI clicks!
            img.color = Color.white;

            var cursorCanvas = cursorGo.AddComponent<Canvas>();
            cursorCanvas.overrideSorting = true;
            cursorCanvas.sortingOrder = 1000;

            cursorGo.AddComponent<Wagenheimer.RewiredHelper.UI.GameCursorPositioner>();

            Undo.RegisterCreatedObjectUndo(cursorGo, "Create Game Cursor");

            if (serializedObject != null)
            {
                var prop = serializedObject.FindProperty("GameCursor");
                if (prop != null)
                {
                    prop.objectReferenceValue = img;
                    serializedObject.ApplyModifiedProperties();
                }
            }

            Selection.activeGameObject = cursorGo;
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Created custom Game Cursor UI Image (raycastTarget=false) and linked to GameCursor field.");
        }

        const string PlayerMouseTypeName = "Rewired.Components.PlayerMouse";

        internal static Type FindPlayerMouseType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(PlayerMouseTypeName);
                if (type != null) return type;
            }
            return null;
        }

        internal static Component FindPlayerMouseInScene(Type playerMouseType)
        {
            return playerMouseType != null ? UnityEngine.Object.FindObjectOfType(playerMouseType) as Component : null;
        }

        /// <summary>
        /// Counts axis elements actually bound to a Rewired action inside PlayerMouse's Elements
        /// list (private serialized field "_elements", verified against Rewired's own shipped
        /// PlayerMouseUnityUI example). Returns -1 if the field couldn't be found (different
        /// Rewired version) so callers don't report a false failure.
        /// </summary>
        internal static int CountConfiguredMouseElements(SerializedObject playerMouseSerialized)
        {
            var groups = playerMouseSerialized.FindProperty("_elements");
            if (groups == null) return -1;

            int count = 0;
            for (int i = 0; i < groups.arraySize; i++)
            {
                var nested = groups.GetArrayElementAtIndex(i).FindPropertyRelative("_elements");
                if (nested == null) continue;

                for (int j = 0; j < nested.arraySize; j++)
                {
                    var actionIdProp = nested.GetArrayElementAtIndex(j).FindPropertyRelative("_actionId");
                    if (actionIdProp != null && actionIdProp.intValue >= 0)
                        count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Adds a "Movement" element group bound to the MouseX/MouseY actions (the same action
        /// names RewiredInputManager already reads via Player.GetAxis) — same shape as Rewired's
        /// own PlayerMouseUnityUI example. Only appends; never touches existing groups.
        /// </summary>
        internal static void ConfigureMouseMovementElements(Component playerMouse)
        {
            var so = new SerializedObject(playerMouse);
            var groups = so.FindProperty("_elements");
            if (groups == null)
            {
                Debug.LogWarning("[RewiredHelper] Could not find Player Mouse's Elements field — Rewired version may differ from the one this was built against.");
                return;
            }

            int horizontalId = ResolveActionId("MouseX");
            int verticalId = ResolveActionId("MouseY");

            int groupIndex = groups.arraySize;
            groups.arraySize++;
            var group = groups.GetArrayElementAtIndex(groupIndex);
            group.FindPropertyRelative("_name").stringValue = "Movement";
            group.FindPropertyRelative("_elementType").intValue = 101;
            group.FindPropertyRelative("_enabled").boolValue = true;

            var nested = group.FindPropertyRelative("_elements");
            nested.arraySize = 2;
            SetAxisElement(nested.GetArrayElementAtIndex(0), "Horizontal", horizontalId);
            SetAxisElement(nested.GetArrayElementAtIndex(1), "Vertical", verticalId);

            so.ApplyModifiedProperties();
            Undo.RegisterCompleteObjectUndo(playerMouse, "Configure Player Mouse Movement");
            MarkSceneDirty();

            Debug.Log("[RewiredHelper] Added a 'Movement' element group to Player Mouse, bound to the MouseX/MouseY actions.");
        }

        private static void SetAxisElement(SerializedProperty element, string name, int actionId)
        {
            element.FindPropertyRelative("_name").stringValue = name;
            element.FindPropertyRelative("_elementType").intValue = 2; // Axis
            element.FindPropertyRelative("_enabled").boolValue = true;
            element.FindPropertyRelative("_actionId").intValue = actionId;
            element.FindPropertyRelative("_coordinateMode").intValue = 1; // Relative
            element.FindPropertyRelative("_absoluteToRelativeSensitivity").floatValue = 600f;
            element.FindPropertyRelative("_repeatRate").floatValue = 4f;
        }

        private static int ResolveActionId(string actionName)
        {
            var action = ReInput.mapping != null ? ReInput.mapping.GetAction(actionName) : null;
            return action != null ? action.id : -1;
        }

        internal static void CreatePlayerMouseAndWire(RewiredInputManager manager)
        {
            var type = FindPlayerMouseType();
            if (type == null)
            {
                Debug.LogWarning("[RewiredHelper] Rewired.Components.PlayerMouse type not found — check your Rewired installation.");
                return;
            }

            var go = new GameObject("PlayerMouse_Player0");
            var comp = go.AddComponent(type);
            Undo.RegisterCreatedObjectUndo(go, "Create Player Mouse");

            var so = new SerializedObject(comp);
            SetIfPresent(so, "_rewiredInputManager", FindInputManagerInScene());
            SetIfPresent(so, "_playerId", 0);
            SetIfPresent(so, "_pointerSpeed", 1f);
            SetIfPresent(so, "_useHardwarePointerPosition", true);
            SetIfPresent(so, "_clampToMovementArea", true);
            SetIfPresent(so, "_defaultToCenter", true);
            so.ApplyModifiedProperties();

            ConfigureMouseMovementElements(comp);

            Selection.activeGameObject = go;
            MarkSceneDirty();
            Debug.Log("[RewiredHelper] Created Player Mouse (Rewired.Components.PlayerMouse) with Movement elements bound to MouseX/MouseY.");
        }

        private static void SetIfPresent(SerializedObject so, string propertyName, object value)
        {
            var prop = so.FindProperty(propertyName);
            if (prop == null) return;

            switch (value)
            {
                case bool b: prop.boolValue = b; break;
                case int i: prop.intValue = i; break;
                case float f: prop.floatValue = f; break;
                case Component c: prop.objectReferenceValue = c; break;
            }
        }

        private static GameObject CreatePauseScreen(Transform parent)
        {
            var pauseGo = new GameObject("PauseScreen", typeof(RectTransform), typeof(Image));
            var pauseRect = (RectTransform)pauseGo.transform;
            pauseRect.SetParent(parent, false);
            pauseRect.anchorMin = Vector2.zero;
            pauseRect.anchorMax = Vector2.one;
            pauseRect.sizeDelta = Vector2.zero;
            pauseRect.anchoredPosition = Vector2.zero;

            var bg = pauseGo.GetComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.75f);

            var titleGo = new GameObject("PauseTitle", typeof(RectTransform));
            var titleRect = (RectTransform)titleGo.transform;
            titleRect.SetParent(pauseRect, false);
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(400, 100);
            titleRect.anchoredPosition = new Vector2(0, 50);

            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "GAME PAUSED";
            titleText.fontSize = 40;
            titleText.fontStyle = FontStyles.Bold;
            titleText.color = Color.white;
            titleText.alignment = TextAlignmentOptions.Center;

            var subGo = new GameObject("PauseSubtitle", typeof(RectTransform));
            var subRect = (RectTransform)subGo.transform;
            subRect.SetParent(pauseRect, false);
            subRect.anchorMin = new Vector2(0.5f, 0.5f);
            subRect.anchorMax = new Vector2(0.5f, 0.5f);
            subRect.sizeDelta = new Vector2(400, 50);
            subRect.anchoredPosition = new Vector2(0, -20);

            var subText = subGo.AddComponent<TextMeshProUGUI>();
            subText.text = "Press ESC or Menu button to resume";
            subText.fontSize = 18;
            subText.color = new Color(0.7f, 0.7f, 0.75f);
            subText.alignment = TextAlignmentOptions.Center;

            pauseGo.SetActive(false);
            return pauseGo;
        }

        static GameObject GenerateRowBasedHelpForm(Transform parent)
        {
            // 1. Create Root Backdrop Panel (Stretches full screen)
            var formGo = new GameObject("ControllerHelpForm", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
            var formRect = (RectTransform)formGo.transform;
            formRect.SetParent(parent, false);
            formRect.anchorMin = Vector2.zero;
            formRect.anchorMax = Vector2.one;
            formRect.sizeDelta = Vector2.zero;
            formRect.anchoredPosition = Vector2.zero;

            var bgOverlay = formGo.GetComponent<Image>();
            bgOverlay.color = new Color(0f, 0f, 0f, 0.6f); // Semi-transparent black backdrop overlay

            // Add Dialog component from the package
            var modalDialog = formGo.AddComponent<Wagenheimer.RewiredHelper.UI.Dialog>();
            modalDialog.Black = bgOverlay;
            modalDialog.BlackAlpha = 0.6f;
            modalDialog.ShowEffect = Wagenheimer.RewiredHelper.UI.ShowDialogEffect.Fade;
            modalDialog.ShowHideDialogTime = 0.15f;

            // 1b. Create Main Dialog Window Panel (600x450)
            var cardGo = new GameObject("Card", typeof(RectTransform), typeof(Image));
            var cardRect = (RectTransform)cardGo.transform;
            cardRect.SetParent(formRect, false);
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(600, 450);
            cardRect.anchoredPosition = Vector2.zero;

            var bgCard = cardGo.GetComponent<Image>();
            bgCard.color = new Color(0.08f, 0.08f, 0.10f, 0.98f); // Sleek dark blue-black background

            // Top Color Highlight Bar (Accent)
            var topBarGo = new GameObject("TopAccentBar", typeof(RectTransform), typeof(Image));
            var topBarRect = (RectTransform)topBarGo.transform;
            topBarRect.SetParent(cardRect, false);
            topBarRect.anchorMin = new Vector2(0, 1);
            topBarRect.anchorMax = new Vector2(1, 1);
            topBarRect.pivot = new Vector2(0.5f, 1);
            topBarRect.sizeDelta = new Vector2(0, 5);
            topBarRect.anchoredPosition = Vector2.zero;
            topBarGo.GetComponent<Image>().color = new Color(0.22f, 0.60f, 1.00f); // Accent Blue

            // 2. Create Header Title
            var headerGo = new GameObject("HeaderTitle", typeof(RectTransform));
            var headerRect = (RectTransform)headerGo.transform;
            headerRect.SetParent(cardRect, false);
            headerRect.anchorMin = new Vector2(0, 1);
            headerRect.anchorMax = new Vector2(1, 1);
            headerRect.pivot = new Vector2(0.5f, 1);
            headerRect.sizeDelta = new Vector2(0, 45);
            headerRect.anchoredPosition = new Vector2(0, -15);

            var headerText = headerGo.AddComponent<TextMeshProUGUI>();
            headerText.text = "GAMEPAD CONTROLS";
            headerText.fontSize = 20;
            headerText.color = Color.white;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.Center;

            // Separator Underline
            var sepGo = new GameObject("HeaderSeparator", typeof(RectTransform), typeof(Image));
            var sepRect = (RectTransform)sepGo.transform;
            sepRect.SetParent(cardRect, false);
            sepRect.anchorMin = new Vector2(0, 1);
            sepRect.anchorMax = new Vector2(1, 1);
            sepRect.pivot = new Vector2(0.5f, 1);
            sepRect.sizeDelta = new Vector2(-40, 1);
            sepRect.anchoredPosition = new Vector2(0, -60);
            sepGo.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.22f, 1f);

            // 3. Create Scroll View
            var scrollViewGo = new GameObject("Scroll View", typeof(RectTransform), typeof(ScrollRect));
            var scrollRect = scrollViewGo.GetComponent<ScrollRect>();
            var scrollRectTransform = (RectTransform)scrollViewGo.transform;
            scrollRectTransform.SetParent(cardRect, false);
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.offsetMin = new Vector2(20, 50); // Leave room for footer
            scrollRectTransform.offsetMax = new Vector2(-20, -75); // Offset top for header

            // Viewport — RectMask2D instead of Mask+Image: Mask clips via a stencil buffer that a
            // fully transparent Image writes to, and depending on Unity version/render pipeline that
            // write can get skipped or discarded at the shader level even with cullTransparentMesh
            // disabled, silently clipping every child out instead of nothing. RectMask2D clips
            // purely via the shader's _ClipRect, with no Graphic/stencil involved, so it can't hit
            // that failure mode.
            var viewportGo = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D));
            var viewportRect = (RectTransform)viewportGo.transform;
            viewportRect.SetParent(scrollRectTransform, false);
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;

            // Content Container
            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(GridLayoutGroup), typeof(ContentSizeFitter));
            var contentRect = (RectTransform)contentGo.transform;
            contentRect.SetParent(viewportRect, false);
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.sizeDelta = new Vector2(0, 0);

            var glg = contentGo.GetComponent<GridLayoutGroup>();
            glg.cellSize = new Vector2(265, 48);
            glg.spacing = new Vector2(10, 8);
            glg.startCorner = GridLayoutGroup.Corner.UpperLeft;
            glg.startAxis = GridLayoutGroup.Axis.Horizontal;
            glg.childAlignment = TextAnchor.UpperCenter;
            glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            glg.constraintCount = 2;

            var csf = contentGo.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;

            // Footer Prompt
            var footerGo = new GameObject("FooterPrompt", typeof(RectTransform));
            var footerRect = (RectTransform)footerGo.transform;
            footerRect.SetParent(cardRect, false);
            footerRect.anchorMin = new Vector2(0, 0);
            footerRect.anchorMax = new Vector2(1, 0);
            footerRect.pivot = new Vector2(0.5f, 0);
            footerRect.sizeDelta = new Vector2(0, 30);
            footerRect.anchoredPosition = new Vector2(0, 12);

            var footerText = footerGo.AddComponent<TextMeshProUGUI>();
            footerText.text = "PRESS ANY BUTTON TO RESUME";
            footerText.fontSize = 11;
            footerText.color = new Color(0.45f, 0.45f, 0.50f);
            footerText.fontStyle = FontStyles.Bold;
            footerText.alignment = TextAlignmentOptions.Center;

            // 4. Rows are built at runtime (see Wagenheimer.RewiredHelper.UI.ControllerHelpRowBuilder)
            // instead of baked here, because ReInput.mapping.Actions is only populated once Rewired
            // is initialized (normally Play mode) — generating rows at edit time means either stale
            // data or, if the mapping was empty, placeholder rows with fake action names and no
            // glyphs. The runtime component clears and rebuilds from the live action map on Awake
            // every time the game starts, so it always matches the current Rewired configuration.
            contentGo.AddComponent<Wagenheimer.RewiredHelper.UI.ControllerHelpRowBuilder>();

            return formGo;
        }

        static Canvas FindOrCreateCanvas()
        {
            var existing = UnityEngine.Object.FindObjectOfType<Canvas>();
            if (existing != null)
                return existing;

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Canvas");
            return canvas;
        }

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Verify && Fix Game Cursor Wiring", priority = 13)]
        internal static void VerifyAndFixGameCursorWiring()
        {
            var manager = FindInputManagerInScene()?.GetComponent<RewiredInputManager>();
            if (manager == null)
            {
                Debug.LogWarning("[RewiredHelper] No RewiredInputManager found in the scene — nothing to fix.");
                return;
            }

            if (manager.GameCursor == null)
            {
                Debug.LogWarning("[RewiredHelper] RewiredInputManager.GameCursor isn't assigned — assign it first (or run Create Rewired Input Manager's cursor step).");
                return;
            }

            var playerMouseType = FindPlayerMouseType();
            var playerMouse = FindPlayerMouseInScene(playerMouseType);
            if (playerMouse == null)
            {
                Debug.LogWarning("[RewiredHelper] No Rewired.Components.PlayerMouse found in the scene — nothing to re-wire.");
                return;
            }

            WirePlayerMouseEvents(manager, playerMouse);
            Debug.Log("[RewiredHelper] Verified Game Cursor wiring — GameCursorPositioner is present and PlayerMouse's screen-position event points at it.");
        }

        internal static void WirePlayerMouseEvents(RewiredInputManager manager, Component playerMouse)
        {
            if (manager == null || playerMouse == null || manager.GameCursor == null)
            {
                Debug.LogWarning("[RewiredHelper] Cannot wire Player Mouse events: make sure Game Cursor is assigned first.");
                return;
            }

            var so = new SerializedObject(playerMouse);

            // On Screen Position Changed — wire to GameCursorPositioner.SetScreenPosition(Vector2).
            // NOT bound to RectTransform.anchoredPosition directly: PlayerMouse reports raw screen
            // pixels, which only line up with anchoredPosition when the Canvas is Screen Space -
            // Overlay AND its Canvas Scaler is 1:1 with the real resolution. Any other setup
            // (Screen Space - Camera, World Space, or a scaled reference resolution) needs the
            // screen point converted through the Canvas first, which the positioner does.
            var positioner = manager.GameCursor.GetComponent<Wagenheimer.RewiredHelper.UI.GameCursorPositioner>();
            if (positioner == null)
            {
                positioner = manager.GameCursor.gameObject.AddComponent<Wagenheimer.RewiredHelper.UI.GameCursorPositioner>();
                Undo.RegisterCreatedObjectUndo(positioner, "Add Game Cursor Positioner");
                Debug.Log("[RewiredHelper] Added missing GameCursorPositioner to Game Cursor.");
            }

            var screenPosCalls = so.FindProperty("_onScreenPositionChanged.m_PersistentCalls.m_Calls");
            if (screenPosCalls != null)
            {
                const string methodName = "SetScreenPosition";
                PruneNullSerializedListeners(screenPosCalls);
                RemoveSerializedListenersToOtherTargets(screenPosCalls, positioner);
                if (!HasSerializedListener(screenPosCalls, positioner, methodName))
                {
                    AppendSerializedListener(
                        screenPosCalls,
                        target: positioner,
                        assemblyTypeName: $"{typeof(Wagenheimer.RewiredHelper.UI.GameCursorPositioner).FullName}, {typeof(Wagenheimer.RewiredHelper.UI.GameCursorPositioner).Assembly.GetName().Name}",
                        methodName: methodName,
                        mode: 0 // EventDefined (dynamic — matches the Vector2 the event passes)
                    );
                }
            }

            // On Enabled State Changed — wire to GameObject.SetActive(bool)
            var enabledCalls = so.FindProperty("_onEnabledStateChanged.m_PersistentCalls.m_Calls");
            if (enabledCalls != null)
            {
                PruneNullSerializedListeners(enabledCalls);
                if (!HasSerializedListener(enabledCalls, manager.GameCursor.gameObject, "SetActive"))
                {
                    AppendSerializedListener(
                        enabledCalls,
                        target: manager.GameCursor.gameObject,
                        assemblyTypeName: $"{typeof(GameObject).FullName}, UnityEngine",
                        methodName: "SetActive",
                        mode: 0 // EventDefined (dynamic — matches the bool the event passes)
                    );
                }
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(playerMouse);
            MarkSceneDirty();
            Debug.Log("[RewiredHelper] Player Mouse events auto-wired to Game Cursor (anchoredPosition & SetActive).");
        }

        private static void PruneNullListeners(UnityEngine.Events.UnityEventBase ev)
        {
            for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                if (ev.GetPersistentTarget(i) == null)
                    UnityEditor.Events.UnityEventTools.RemovePersistentListener(ev, i);
            }
        }

        private static bool HasListener(UnityEngine.Events.UnityEventBase ev, UnityEngine.Object target, string methodName)
        {
            for (int i = 0; i < ev.GetPersistentEventCount(); i++)
            {
                if (ev.GetPersistentTarget(i) == target && ev.GetPersistentMethodName(i) == methodName)
                    return true;
            }
            return false;
        }

        private static void PruneNullSerializedListeners(SerializedProperty callsArray)
        {
            for (int i = callsArray.arraySize - 1; i >= 0; i--)
            {
                var target = callsArray.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target").objectReferenceValue;
                if (target == null)
                    callsArray.DeleteArrayElementAtIndex(i);
            }
        }

        /// <summary>
        /// Strips any persistent listener that doesn't target <paramref name="keepTarget"/> — used
        /// to clear out a stale direct-to-anchoredPosition binding left over from an older wiring
        /// (or an earlier RewiredHelper version) before re-adding the correct one.
        /// </summary>
        private static void RemoveSerializedListenersToOtherTargets(SerializedProperty callsArray, UnityEngine.Object keepTarget)
        {
            for (int i = callsArray.arraySize - 1; i >= 0; i--)
            {
                var target = callsArray.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target").objectReferenceValue;
                if (target != keepTarget)
                    callsArray.DeleteArrayElementAtIndex(i);
            }
        }

        private static bool HasSerializedListener(SerializedProperty callsArray, UnityEngine.Object target, string methodName)
        {
            for (int i = 0; i < callsArray.arraySize; i++)
            {
                var el = callsArray.GetArrayElementAtIndex(i);
                var t = el.FindPropertyRelative("m_Target").objectReferenceValue;
                var m = el.FindPropertyRelative("m_MethodName").stringValue;
                if (t == target && m == methodName)
                    return true;
            }
            return false;
        }

        private static void AppendSerializedListener(SerializedProperty callsArray, UnityEngine.Object target, string assemblyTypeName, string methodName, int mode)
        {
            callsArray.arraySize++;
            var call = callsArray.GetArrayElementAtIndex(callsArray.arraySize - 1);

            call.FindPropertyRelative("m_Target").objectReferenceValue = target;
            call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = assemblyTypeName;
            call.FindPropertyRelative("m_MethodName").stringValue = methodName;
            call.FindPropertyRelative("m_Mode").intValue = mode;
            call.FindPropertyRelative("m_CallState").intValue = 2; // EditorAndRuntime

            // Zero-out argument fields (avoid leftover junk from array expansion)
            var args = call.FindPropertyRelative("m_Arguments");
            if (args != null)
            {
                args.FindPropertyRelative("m_ObjectArgument").objectReferenceValue = null;
                args.FindPropertyRelative("m_ObjectArgumentAssemblyTypeName").stringValue = "UnityEngine.Object, UnityEngine";
                args.FindPropertyRelative("m_IntArgument").intValue = 0;
                args.FindPropertyRelative("m_FloatArgument").floatValue = 0f;
                args.FindPropertyRelative("m_StringArgument").stringValue = string.Empty;
                args.FindPropertyRelative("m_BoolArgument").boolValue = false;
            }
        }

        private static void AddPersistentListenerReflected<T>(UnityEngine.Events.UnityEventBase ev, UnityEngine.Events.UnityAction<T> action)
        {
            // UnityEventTools.AddPersistentListener(UnityEvent<T>, UnityAction<T>) is only accessible
            // via the concrete generic type. We call it through reflection to decouple from Rewired's
            // internal event subclass at compile time.
            var addMethod = typeof(UnityEditor.Events.UnityEventTools)
                .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "AddPersistentListener" || !m.IsGenericMethodDefinition) return false;
                    var p = m.GetParameters();
                    return p.Length == 2 && p[0].ParameterType.IsGenericType && p[1].ParameterType.IsGenericType;
                });

            if (addMethod == null)
            {
                Debug.LogWarning("[RewiredHelper] Could not find UnityEventTools.AddPersistentListener<T> — check Unity version compatibility.");
                return;
            }

            var genericMethod = addMethod.MakeGenericMethod(typeof(T));
            genericMethod.Invoke(null, new object[] { ev, action });
        }

        static void MarkSceneDirty()
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        internal static Component FindInputManagerInScene()
        {
            foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                var comp = go.GetComponent("InputManager");
                if (comp != null && comp.GetType().FullName == "Rewired.InputManager")
                    return comp;
            }
            return null;
        }
    }
}
