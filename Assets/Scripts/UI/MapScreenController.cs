using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Metroidbrainia
{
    public enum MapTab
    {
        Map,
        Discoveries
    }

    // After PauseSystem resolves Escape/Start, before gameplay processes input.
    [DefaultExecutionOrder(-90)]
    [DisallowMultipleComponent]
    public sealed class MapScreenController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject mapRoot;
        [SerializeField] private GameObject mapPage;
        [SerializeField] private GameObject discoveryPage;
        [SerializeField] private Button mapTabButton;
        [SerializeField] private Button discoveryTabButton;
        [SerializeField] private RectTransform mapCursor;
        [SerializeField] private RectTransform cursorBounds;
        [SerializeField] private PauseMenuController pauseController;

        [Header("Cursor")]
        [SerializeField, Min(0f)] private float gamepadCursorSpeed = 900f;
        [SerializeField, Range(0f, 0.99f)] private float stickDeadzone = 0.15f;

        private InputActionMap mapActions;
        private InputAction toggleMap;
        private InputAction previousTab;
        private InputAction nextTab;
        private InputAction cursorMove;
        private InputAction submit;
        private readonly List<RaycastResult> submitHits = new List<RaycastResult>();
        private EventSystem submitEventSystem;
        private PointerEventData submitPointer;
        private readonly Vector3[] cursorCorners = new Vector3[4];
        private Vector2 cursorPosition;
        private Vector2 previousMousePosition;
        private Mouse trackedMouse;
        private bool cursorInitialized;
        private bool initialized;
        private GraphicRaycaster contentRaycaster;
        private bool raycasterWasEnabled;
        private bool mapTabWasInteractable;
        private bool discoveryTabWasInteractable;

        public bool IsOpen { get; private set; }
        public bool IsContentInteractionBlocked { get; private set; }
        public MapTab CurrentTab { get; private set; } = MapTab.Map;
        public event Action MapClosed;

        private void Awake()
        {
            mapActions = new InputActionMap("Map");
            toggleMap = mapActions.AddAction("ToggleMap", InputActionType.Button);
            toggleMap.AddBinding("<Keyboard>/m");
            toggleMap.AddBinding("<Gamepad>/select");
            previousTab = mapActions.AddAction("PreviousTab", InputActionType.Button, "<Gamepad>/leftShoulder");
            nextTab = mapActions.AddAction("NextTab", InputActionType.Button, "<Gamepad>/rightShoulder");
            cursorMove = mapActions.AddAction("CursorMove", InputActionType.Value, "<Gamepad>/leftStick");
            submit = mapActions.AddAction("Submit", InputActionType.Button, "<Gamepad>/buttonSouth");
        }

        private void OnEnable()
        {
            mapActions.Enable();
        }

        private void Start()
        {
            Canvas canvas = mapRoot != null ? mapRoot.GetComponent<Canvas>() : null;
            if (mapRoot == null || mapPage == null || discoveryPage == null
                || mapTabButton == null || discoveryTabButton == null || mapCursor == null
                || cursorBounds == null || pauseController == null || canvas == null
                || canvas.renderMode != RenderMode.ScreenSpaceOverlay
                || mapRoot == gameObject || transform.IsChildOf(mapRoot.transform)
                || !mapCursor.IsChildOf(mapRoot.transform) || !cursorBounds.IsChildOf(mapRoot.transform))
            {
                Debug.LogError("Assign the Overlay Canvas Map, both pages/buttons, MapCursor, MapFrame and PauseSystem. Keep MapSystem outside Canvas Map.", this);
                enabled = false;
                return;
            }

            mapTabButton.onClick.AddListener(ShowMapTab);
            discoveryTabButton.onClick.AddListener(ShowDiscoveriesTab);
            contentRaycaster = canvas.GetComponent<GraphicRaycaster>();
            initialized = true;
            ApplyTab();
            mapRoot.SetActive(false);
        }

        private void Update()
        {
            if (!initialized || !Application.isFocused)
                return;

            if (IsOpen && (pauseController == null || !pauseController.isActiveAndEnabled
                || mapRoot == null || mapCursor == null || cursorBounds == null))
            {
                CleanupWithoutResume();
                return;
            }

            if (toggleMap.WasPressedThisFrame())
            {
                ToggleMap();
                return;
            }
            if (!IsOpen)
                return;

            if (!IsContentInteractionBlocked
                && (previousTab.WasPressedThisFrame() || nextTab.WasPressedThisFrame()))
                SetTab(CurrentTab == MapTab.Map ? MapTab.Discoveries : MapTab.Map);

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 screenPosition = mouse.position.ReadValue();
                if (mouse == trackedMouse && (screenPosition - previousMousePosition).sqrMagnitude >= 0.25f
                    && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        cursorBounds, screenPosition, null, out Vector2 localPosition))
                    cursorPosition = localPosition;
                previousMousePosition = screenPosition;
            }
            trackedMouse = mouse;

            Vector2 stick = cursorMove.ReadValue<Vector2>();
            if (stick.sqrMagnitude > stickDeadzone * stickDeadzone)
                cursorPosition += Vector2.ClampMagnitude(stick, 1f) * gamepadCursorSpeed * Time.unscaledDeltaTime;
        }

        private void LateUpdate()
        {
            if (!IsOpen || mapCursor == null || cursorBounds == null)
                return;

            ApplyCursorPosition();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = false;
            if (!IsContentInteractionBlocked && Application.isFocused && submit.WasPressedThisFrame())
                SubmitButtonUnderCursor();
        }

        private void SubmitButtonUnderCursor()
        {
            EventSystem currentEventSystem = EventSystem.current;
            if (currentEventSystem == null || mapRoot == null)
                return;

            if (submitPointer == null || submitEventSystem != currentEventSystem)
            {
                submitEventSystem = currentEventSystem;
                submitPointer = new PointerEventData(currentEventSystem);
            }

            submitPointer.Reset();
            submitPointer.position = RectTransformUtility.WorldToScreenPoint(null, mapCursor.position);
            submitHits.Clear();
            currentEventSystem.RaycastAll(submitPointer, submitHits);
            foreach (RaycastResult hit in submitHits)
            {
                if (!(hit.module is GraphicRaycaster) || hit.gameObject == null
                    || !hit.gameObject.transform.IsChildOf(mapRoot.transform))
                    continue;

                Button button = hit.gameObject.GetComponentInParent<Button>();
                if (button == null || !button.IsActive() || !button.IsInteractable())
                    continue;

                button.onClick.Invoke();
                break;
            }
            submitHits.Clear();
        }

        public void ToggleMap()
        {
            if (!initialized || !isActiveAndEnabled)
                return;
            if (IsOpen)
                CloseMap();
            else
                OpenMap();
        }

        public void OpenMap()
        {
            if (!initialized || !isActiveAndEnabled || IsOpen || mapRoot == null || mapCursor == null
                || cursorBounds == null || pauseController == null
                || !pauseController.TryBeginMap(this))
                return;

            IsOpen = true;
            mapRoot.SetActive(true);
            ApplyTab();
            Canvas.ForceUpdateCanvases();
            if (!cursorInitialized)
            {
                cursorPosition = cursorBounds.rect.center;
                cursorInitialized = true;
            }
            trackedMouse = Mouse.current;
            if (trackedMouse != null)
                previousMousePosition = trackedMouse.position.ReadValue();
            ApplyCursorPosition();
        }

        public void CloseMap()
        {
            if (!IsOpen || IsContentInteractionBlocked)
                return;

            IsOpen = false;
            MapClosed?.Invoke();
            if (mapRoot != null)
                mapRoot.SetActive(false);
            SetContentInteractionBlocked(false);
            if (pauseController != null)
                pauseController.EndMap(this);
        }

        public void SetContentInteractionBlocked(bool blocked)
        {
            if (IsContentInteractionBlocked == blocked)
                return;

            IsContentInteractionBlocked = blocked;
            if (blocked)
            {
                mapTabWasInteractable = mapTabButton != null && mapTabButton.interactable;
                discoveryTabWasInteractable = discoveryTabButton != null && discoveryTabButton.interactable;
                raycasterWasEnabled = contentRaycaster != null && contentRaycaster.enabled;
            }
            if (mapTabButton != null)
                mapTabButton.interactable = !blocked && mapTabWasInteractable;
            if (discoveryTabButton != null)
                discoveryTabButton.interactable = !blocked && discoveryTabWasInteractable;
            if (contentRaycaster != null)
                contentRaycaster.enabled = !blocked && raycasterWasEnabled;
        }

        public void ShowMapTab()
        {
            SetTab(MapTab.Map);
        }

        public void ShowDiscoveriesTab()
        {
            SetTab(MapTab.Discoveries);
        }

        private void SetTab(MapTab tab)
        {
            if (!IsOpen || IsContentInteractionBlocked)
                return;
            CurrentTab = tab;
            ApplyTab();
        }

        private void ApplyTab()
        {
            if (mapPage != null)
                mapPage.SetActive(CurrentTab == MapTab.Map);
            if (discoveryPage != null)
                discoveryPage.SetActive(CurrentTab == MapTab.Discoveries);
        }

        private void ApplyCursorPosition()
        {
            // Work in MapFrame units, then convert back to the cursor's actual parent space.
            float localZ = cursorBounds.InverseTransformPoint(mapCursor.position).z;
            mapCursor.position = cursorBounds.TransformPoint(new Vector3(cursorPosition.x, cursorPosition.y, localZ));
            mapCursor.GetWorldCorners(cursorCorners);
            Vector2 minOffset = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maxOffset = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            for (int i = 0; i < cursorCorners.Length; i++)
            {
                Vector2 offset = (Vector2)cursorBounds.InverseTransformPoint(cursorCorners[i]) - cursorPosition;
                minOffset = Vector2.Min(minOffset, offset);
                maxOffset = Vector2.Max(maxOffset, offset);
            }

            Rect bounds = cursorBounds.rect;
            float minX = bounds.xMin - minOffset.x;
            float maxX = bounds.xMax - maxOffset.x;
            float minY = bounds.yMin - minOffset.y;
            float maxY = bounds.yMax - maxOffset.y;
            cursorPosition.x = minX <= maxX ? Mathf.Clamp(cursorPosition.x, minX, maxX) : bounds.center.x;
            cursorPosition.y = minY <= maxY ? Mathf.Clamp(cursorPosition.y, minY, maxY) : bounds.center.y;
            mapCursor.position = cursorBounds.TransformPoint(new Vector3(cursorPosition.x, cursorPosition.y, localZ));
        }

        private void CleanupWithoutResume()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            MapClosed?.Invoke();
            if (mapRoot != null)
                mapRoot.SetActive(false);
            SetContentInteractionBlocked(false);
            if (pauseController != null)
                pauseController.AbandonMap(this);
        }

        private void OnDisable()
        {
            mapActions?.Disable();
            CleanupWithoutResume();
        }

        private void OnDestroy()
        {
            if (mapTabButton != null)
                mapTabButton.onClick.RemoveListener(ShowMapTab);
            if (discoveryTabButton != null)
                discoveryTabButton.onClick.RemoveListener(ShowDiscoveriesTab);
            mapActions?.Dispose();
        }
    }
}
