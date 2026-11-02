using System;
using System.Collections.Generic;
using System.Linq;

using UnityEditor;

using UnityEngine;
using UnityEngine.UIElements;

using Wagenheimer.PackageHub.Editor;

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

        [SerializeField] private Tab _currentTab = Tab.SetupAudit;
        private VisualElement _root;
        private ScrollView _contentContainer;

        private const string PackageId = "com.wagenheimer.rewiredhelper";
        private Button _updateStatus;

        private static readonly Vector2 MinSize = new Vector2(620, 520);
        private static readonly Vector2 DefaultSize = new Vector2(820, 640);

        [MenuItem(RewiredHelperMenu.Dashboard, priority = RewiredHelperMenu.DashboardPriority)]
        [MenuItem("Window/Wagenheimer/Rewired Helper/Dashboard", priority = 212)]
        public static void OpenDashboard() => Open(Tab.SetupAudit);

        /// <summary>Recovery for a window stuck off-screen (e.g. saved on a monitor that is no longer connected).</summary>
        [MenuItem(RewiredHelperMenu.ResetWindowPosition, priority = RewiredHelperMenu.ResetWindowPositionPriority)]
        public static void ResetWindowPosition()
        {
            var window = GetWindow<RewiredHelperDashboardWindow>("Rewired Helper");
            window.minSize = MinSize;
            EditorWindowPlacement.Center(window, MinSize, DefaultSize);
            window.Show();
            window.Focus();
        }

        public static void OpenAuditTab() => Open(Tab.SetupAudit);

        private static void Open(Tab tab)
        {
            var window = GetWindow<RewiredHelperDashboardWindow>("Rewired Helper");
            window.minSize = MinSize;
            window.titleContent = new GUIContent("Rewired Helper", EditorGUIUtility.IconContent("d_Favorite").image);
            window._currentTab = tab;
            EditorWindowPlacement.EnsureOnScreen(window, MinSize, DefaultSize);
            window.Show();
            window.Focus();
            if (window._root != null) window.RebuildUI();
        }

        public void CreateGUI()
        {
            _root = rootVisualElement;
            _root.style.flexGrow = 1;
            RewiredHelperUIStyle.Apply(_root);
            // Gives the window the background/text colour/padding the stylesheet defines for .rh-root.
            _root.AddToClassList("rh-root");
            RebuildUI();
        }

        private void RebuildUI()
        {
            _root.Clear();

            try
            {
                BuildPage();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _root.Clear();
                _root.Add(RewiredHelperUIStyle.CreateCallout(
                    $"The Rewired Helper dashboard failed to build: {ex.GetType().Name}: {ex.Message}\nSee the Console for the full stack trace.",
                    AuditSeverity.Warning));
            }
        }

        private void BuildPage()
        {
            // One full-width column (.rh-page) holding header, tabs and content.
            var page = new VisualElement();
            page.AddToClassList("rh-page");
            _root.Add(page);

            page.Add(CreateHeaderBanner());
            page.Add(CreateTabBar());

            _contentContainer = new ScrollView(ScrollViewMode.Vertical) { style = { flexGrow = 1 } };
            page.Add(_contentContainer);

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
            versionBadge.tooltip = "Installed version of com.wagenheimer.rewiredhelper.";
            left.Add(versionBadge);

            // Live result of "Check Updates" (hidden until the check runs). Clicking it opens the
            // Package Hub on this package so the new tag can be installed in one step.
            _updateStatus = new Button(() => PackageHubWindow.OpenToPackage(PackageId))
            {
                tooltip = "Click to open the Package Hub for this package."
            };
            _updateStatus.AddToClassList("rh-header-update");
            _updateStatus.style.display = DisplayStyle.None;
            left.Add(_updateStatus);

            row.Add(left);

            var toolbar = new VisualElement();
            toolbar.AddToClassList("rh-toolbar-actions");
            var checkUpdates = RewiredHelperUIStyle.CreateButton("🔄 Check Updates", CheckForUpdates);
            checkUpdates.tooltip = "Compares the installed version with the latest GitHub tag and shows the result here.";
            toolbar.Add(checkUpdates);
            row.Add(toolbar);
            banner.Add(row);

            var subtitle = new Label("Input-type detection, cursor, Escape routing and a mobile-safe pause policy on top of Rewired.");
            subtitle.AddToClassList("rh-header-subtitle");
            banner.Add(subtitle);

            return banner;
        }

        /// <summary>
        /// Checks the latest GitHub tag through the Package Hub's update service and shows the result
        /// next to the version badge, so the installed version and the available one are visible together.
        /// </summary>
        private void CheckForUpdates()
        {
            SetUpdateStatus("checking…", "rh-update-checking");

            PackageItem item = null;
            try
            {
                item = PackageDiscovery.GetAllPackages()?.FirstOrDefault(p => p.PackageId == PackageId);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RewiredHelper] Could not list package versions: {ex.Message}");
            }

            if (item == null)
            {
                SetUpdateStatus("Package Hub unavailable", "rh-update-available");
                PackageHubWindow.OpenToPackage(PackageId);
                return;
            }

            // Compare against what is actually installed; the discovery snapshot can be stale.
            item.IsInstalled = true;
            item.InstalledVersion = GetPackageVersion();

            PackageUpdateService.CheckUpdates(new List<PackageItem> { item }, () => OnUpdateChecked(item));
        }

        private void OnUpdateChecked(PackageItem item)
        {
            if (item == null)
            {
                SetUpdateStatus(null, null);
                return;
            }

            if (string.IsNullOrEmpty(item.LatestRemoteVersion))
            {
                SetUpdateStatus("could not check", "rh-update-available");
                return;
            }

            SetUpdateStatus(
                item.HasUpdate ? $"v{item.LatestRemoteVersion} available" : "up to date",
                item.HasUpdate ? "rh-update-available" : "rh-update-current");
        }

        private void SetUpdateStatus(string text, string stateClass)
        {
            if (_updateStatus == null) return;

            _updateStatus.text = text ?? string.Empty;
            _updateStatus.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;

            _updateStatus.RemoveFromClassList("rh-update-checking");
            _updateStatus.RemoveFromClassList("rh-update-current");
            _updateStatus.RemoveFromClassList("rh-update-available");
            if (!string.IsNullOrEmpty(stateClass)) _updateStatus.AddToClassList(stateClass);
        }

        private VisualElement CreateTabBar()
        {
            var bar = new VisualElement();
            bar.AddToClassList("rh-tab-row");

            (Tab tab, string icon, string title)[] tabs =
            {
                (Tab.SetupAudit, "🔍", "Setup Audit"),
                (Tab.MobileAndPause, "⚙️", "Automatic"),
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
                });
                // Leading emoji in Button.text overlaps the label on Windows; give the icon its own box.
                RewiredHelperUIStyle.ApplyIconText(button, $"{icon} {title}");

                button.AddToClassList("rh-tab-btn");
                if (_currentTab == tab) button.AddToClassList("active");
                bar.Add(button);
            }

            return bar;
        }

        private void RebuildContent()
        {
            _contentContainer.Clear();

            VisualElement view;
            try
            {
                view = _currentTab switch
                {
                    Tab.SetupAudit => new RewiredHelperAuditView().Root,
                    Tab.MobileAndPause => new RewiredHelperMobileView().Root,
                    Tab.Checklist => new RewiredHelperChecklistView().Root,
                    _ => new RewiredHelperDocsView().Root
                };
            }
            catch (Exception ex)
            {
                // A throwing tab constructor used to leave the window blank with only a console error.
                Debug.LogException(ex);
                view = RewiredHelperUIStyle.CreateCallout(
                    $"The '{_currentTab}' tab failed to build: {ex.GetType().Name}: {ex.Message}\nSee the Console for the full stack trace.",
                    AuditSeverity.Warning);
            }

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
