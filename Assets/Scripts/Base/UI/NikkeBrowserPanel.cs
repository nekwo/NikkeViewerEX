using NikkeViewerEX.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NikkeViewerEX.UI
{
    [AddComponentMenu("Nikke Viewer EX/UI/Nikke Browser Panel")]
    [RequireComponent(typeof(UIDocument))]
    public partial class NikkeBrowserPanel : MonoBehaviour
    {
        [Header("Templates")]
        [SerializeField]
        VisualTreeAsset m_BrowserItemTemplate;

        [SerializeField]
        VisualTreeAsset m_ActiveItemTemplate;

        // UI root references
        VisualElement root;
        VisualElement panel;

        // Drag state
        bool dragging;
        Vector2 dragStartPointer;
        Vector2 dragStartPanelPos;

        // Tab buttons
        Button tabConfigBtn;
        Button tabBrowserBtn;
        Button tabActiveBtn;
        Button tabDebugBtn;
        Button tabPresetsBtn;
        Button tabBackgroundsBtn;

        // Tab content panels
        VisualElement contentConfig;
        VisualElement contentBrowser;
        VisualElement contentActive;
        VisualElement contentDebug;
        VisualElement contentBackgrounds;
        VisualElement contentPresets;

        #region Lifecycle
        void Awake()
        {
            mainControl = FindObjectsByType<MainControl>(FindObjectsSortMode.None)[0];
            settingsManager = FindObjectsByType<SettingsManager>(FindObjectsSortMode.None)[0];
        }

        void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            root = doc.rootVisualElement;
            QueryElements();
            BindEvents();
            RebuildActiveViewers();
            RestoreConfig();
        }

        void OnDisable()
        {
            UnbindEvents();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                TogglePanel();
        }
        #endregion

        #region UI Queries
        void QueryElements()
        {
            panel = root.Q("browser-panel");

            tabConfigBtn = root.Q<Button>("tab-config");
            tabBrowserBtn = root.Q<Button>("tab-browser");
            tabActiveBtn = root.Q<Button>("tab-active");
            tabDebugBtn = root.Q<Button>("tab-debug");
            tabPresetsBtn = root.Q<Button>("tab-presets");
            tabBackgroundsBtn = root.Q<Button>("tab-backgrounds");

            contentConfig = root.Q("content-config");
            contentBrowser = root.Q("content-browser");
            contentActive = root.Q("content-active");
            contentDebug = root.Q("content-debug");
            contentBackgrounds = root.Q("content-backgrounds");
            contentPresets = root.Q("content-presets");

            QueryConfigElements();
            QueryBrowserElements();
            QueryActiveElements();
            QueryDebugElements();
            QueryBackgroundElements();
            QueryPresetElements();
        }
        #endregion

        #region Event Binding
        void BindEvents()
        {
            var header = root.Q("header");
            header.pickingMode = PickingMode.Position;
            header.RegisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            header.RegisterCallback<PointerMoveEvent>(OnHeaderPointerMove);
            header.RegisterCallback<PointerUpEvent>(OnHeaderPointerUp);

            tabConfigBtn.clicked += () => SwitchTab(0);
            tabBrowserBtn.clicked += () => SwitchTab(1);
            tabActiveBtn.clicked += () => SwitchTab(2);
            tabDebugBtn.clicked += () => SwitchTab(3);
            tabPresetsBtn.clicked += () => SwitchTab(4);
            tabBackgroundsBtn.clicked += () => SwitchTab(5);

            BindConfigEvents();
            BindBrowserEvents();
            BindActiveEvents();
            BindBackgroundEvents();
            BindPresetEvents();
        }

        void UnbindEvents()
        {
            var header = root.Q("header");
            header.UnregisterCallback<PointerDownEvent>(OnHeaderPointerDown);
            header.UnregisterCallback<PointerMoveEvent>(OnHeaderPointerMove);
            header.UnregisterCallback<PointerUpEvent>(OnHeaderPointerUp);

            UnbindBrowserEvents();
        }

        void OnHeaderPointerDown(PointerDownEvent evt)
        {
            dragging = true;
            dragStartPointer = evt.position;
            dragStartPanelPos = new Vector2(panel.resolvedStyle.left, panel.resolvedStyle.top);
            (evt.target as VisualElement)?.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        void OnHeaderPointerMove(PointerMoveEvent evt)
        {
            if (!dragging) return;
            Vector2 delta = (Vector2)evt.position - dragStartPointer;
            panel.style.left = dragStartPanelPos.x + delta.x;
            panel.style.top  = dragStartPanelPos.y + delta.y;
            evt.StopPropagation();
        }

        void OnHeaderPointerUp(PointerUpEvent evt)
        {
            if (!dragging) return;
            dragging = false;
            (evt.target as VisualElement)?.ReleasePointer(evt.pointerId);
            evt.StopPropagation();
        }
        #endregion

        #region Tab Switching
        void SwitchTab(int index)
        {
            tabConfigBtn.RemoveFromClassList("tab-active");
            tabBrowserBtn.RemoveFromClassList("tab-active");
            tabActiveBtn.RemoveFromClassList("tab-active");
            tabDebugBtn.RemoveFromClassList("tab-active");
            tabPresetsBtn.RemoveFromClassList("tab-active");
            tabBackgroundsBtn.RemoveFromClassList("tab-active");

            contentConfig.RemoveFromClassList("tab-visible");
            contentBrowser.RemoveFromClassList("tab-visible");
            contentActive.RemoveFromClassList("tab-visible");
            contentDebug.RemoveFromClassList("tab-visible");
            contentBackgrounds.RemoveFromClassList("tab-visible");
            contentPresets.RemoveFromClassList("tab-visible");

            switch (index)
            {
                case 0:
                    tabConfigBtn.AddToClassList("tab-active");
                    contentConfig.AddToClassList("tab-visible");
                    break;
                case 1:
                    tabBrowserBtn.AddToClassList("tab-active");
                    contentBrowser.AddToClassList("tab-visible");
                    break;
                case 2:
                    tabActiveBtn.AddToClassList("tab-active");
                    contentActive.AddToClassList("tab-visible");
                    RefreshActiveList();
                    break;
                case 3:
                    tabDebugBtn.AddToClassList("tab-active");
                    contentDebug.AddToClassList("tab-visible");
                    RefreshDebugList();
                    break;
                case 4:
                    tabPresetsBtn.AddToClassList("tab-active");
                    contentPresets.AddToClassList("tab-visible");
                    RefreshPresetList();
                    break;
                case 5:
                    tabBackgroundsBtn.AddToClassList("tab-active");
                    contentBackgrounds.AddToClassList("tab-visible");
                    RefreshBackgroundList();
                    break;
            }
        }
        #endregion

        #region Public API
        public void TogglePanel()
        {
            root.style.display =
                root.style.display == DisplayStyle.None
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        public void Show() => root.style.display = DisplayStyle.Flex;
        public void Hide() => root.style.display = DisplayStyle.None;
        #endregion
    }
}
