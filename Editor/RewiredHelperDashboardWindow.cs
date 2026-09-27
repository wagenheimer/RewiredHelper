using System;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

namespace Wagenheimer.RewiredHelper.Editor
{
    /// <summary>
    /// Unified dashboard for Rewired Helper: setup audit with one-click fixes, mobile/pause policy,
    /// persistent release checklist, and docs/updates. Built with UI Toolkit like the sibling packages.
    /// </summary>
    public class RewiredHelperDashboardWindow : EditorWindow
    {
        private enum Tab
        {
            SetupAudit,
            MobileAndPause,
            Checklist,
            DocsAndUpdates
        }

        private Tab _currentTab = Tab.SetupAudit;
        private VisualElement _root;
        private ScrollView _contentContainer;

        [MenuItem("Tools/Wagenheimer/Rewired Helper/Dashboard...", priority = 138)]
        [MenuItem("Window/Wagenheimer/Rewired Helper/Dashboard", priority = 212)]
        public static void OpenDashboard() => Open(Tab.SetupAudit);

        public static void OpenAuditTab() => Open(Tab.SetupAudit);

        private static void Open(Tab tab)
        {
            var window = GetWindow<RewiredHelperDashboardWindow>("Rewired Helper");
            window.minSize = new Vector2(620, 520);
            window.titleContent = new GUIContent("Rewired Helper", EditorGUIUtility.IconContent("d_Favorite").image);
            window._currentTab = tab;
            window.Show();
            if (window._root != null) window.RebuildUI();
        }

        public void CreateGUI()
        {
            _root = rootVisualElement;
            _root.style.flexGrow = 1;
            RewiredHelperUIStyle.Apply(_root);
            RebuildUI();
        }

        private void RebuildUI()
        {
            _root.Clear();
            _root.Add(CreateHeaderBanner());
            _root.Add(CreateTabBar());

            _contentContainer = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            _root.Add(_contentContainer);

            RebuildContent();
        }

        private VisualElement CreateHeaderBanner()
        {
            var banner = new VisualElement();
            banner.AddToClassList("rh-header");

            var row = new VisualElement();
            row.AddToClassList("rh-header-row");

            var left = new VisualElement();
            left.AddToClassList("rh-header-left");
            left.Add(new Label("🎮") { style = { fontSize = 20, marginRight = 8 } });

            var title = new Label("Rewired Helper");
            title.AddToClassList("rh-header-title");
            left.Add(title);

            var versionBadge = new Label("v" + GetPackageVersion());
            versionBadge.AddToClassList("rh-header-version");
            left.Add(versionBadge);
            row.Add(left);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("rh-toolbar-actions");
            toolbar.Add(RewiredHelperUIStyle.CreateButton("🔄 Updates", () => UpdateChecker.CheckForUpdate(force: true)));
            row.Add(toolbar);
            banner.Add(row);

            var subtitle = new Label("Input-type detection, cursor, Escape routing and a mobile-safe pause policy on top of Rewired.");
            subtitle.AddToClassList("rh-header-subtitle");
            banner.Add(subtitle);

            return banner;
        }

        private VisualElement CreateTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("rh-tab-row");

            (Tab tab, string icon, string title)[] tabs =
            {
                (Tab.SetupAudit, "🔍", "Setup Audit"),
                (Tab.MobileAndPause, "📱", "Mobile & Pause"),
                (Tab.Checklist, "📋", "Checklist"),
                (Tab.DocsAndUpdates, "📚", "Docs & Updates")
            };

            foreach (var (tab, icon, title) in tabs)
            {
                var tabValue = tab;
                var button = new Button(() =>
                {
                    _currentTab = tabValue;
                    RebuildUI();
                })
                { text = $"{icon} {title}" };

                button.AddToClassList("rh-tab-btn");
                if (_currentTab == tab) button.AddToClassList("active");
                bar.Add(button);
            }

            return bar;
        }

        private void RebuildContent()
        {
            _contentContainer.Clear();

            VisualElement view = _currentTab switch
            {
                Tab.SetupAudit => new RewiredHelperAuditView().Root,
                Tab.MobileAndPause => new RewiredHelperMobileView().Root,
                Tab.Checklist => new RewiredHelperChecklistView().Root,
                _ => new RewiredHelperDocsView().Root
            };
            _contentContainer.Add(view);
        }

        internal static string GetPackageVersion()
        {
            try
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(RewiredHelperDashboardWindow).Assembly);
                if (package != null && !string.IsNullOrEmpty(package.version))
                    return package.version;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RewiredHelper] Could not read package version: {ex.Message}");
            }
            return "?";
        }
    }
}
