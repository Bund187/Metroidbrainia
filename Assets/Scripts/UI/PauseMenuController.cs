using System.Collections;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;

namespace Metroidbrainia
{
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject pauseCanvas;
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private StarterAssetsInputs starterInputs;
        [SerializeField] private FirstPersonController firstPersonController;
        [SerializeField] private FirstPersonArmController armController;
        [SerializeField] private EventSystem eventSystem;
        [SerializeField] private InputSystemUIInputModule uiInputModule;
        [SerializeField] private Button firstSelectedButton;

        private readonly List<InputAction> enabledGameplayActions = new List<InputAction>();
        private InputActionAsset menuActions;
        private InputActionAsset previousUIActions;
        private bool controllerWasEnabled;
        private bool armWasEnabled;
        private bool inputWasActive;
        private bool cursorWasLocked;
        private bool cursorLookWasEnabled;
        private bool uiWasEnabled;
        private bool gamepadConnected;
        private bool menuDevicesChanged;
        private bool initialized;

        public bool IsPaused { get; private set; }

        private void Start()
        {
            if (pauseCanvas == null || playerInput == null || starterInputs == null
                || firstPersonController == null || armController == null || eventSystem == null
                || uiInputModule == null || uiInputModule.actionsAsset == null
                || uiInputModule.move == null || uiInputModule.submit == null
                || uiInputModule.point == null || uiInputModule.leftClick == null
                || uiInputModule.move.action == null || uiInputModule.submit.action == null
                || uiInputModule.point.action == null || uiInputModule.leftClick.action == null
                || pauseCanvas == gameObject || transform.IsChildOf(pauseCanvas.transform))
            {
                Debug.LogError("Assign the pause canvas, gameplay components, EventSystem and configured UI module. Keep PauseSystem outside the pause canvas.", this);
                enabled = false;
                return;
            }

            // Keep device filtering separate from PlayerInput and never edit the source asset.
            menuActions = Instantiate(uiInputModule.actionsAsset);
            menuActions.Disable();
            InputAction navigation = menuActions.FindAction(uiInputModule.move.action.id);
            if (navigation != null)
            {
                for (int i = 0; i < navigation.bindings.Count; i++)
                {
                    if (navigation.bindings[i].path.Contains("rightStick"))
                        navigation.ApplyBindingOverride(i, string.Empty);
                }
            }

            initialized = true;
            pauseCanvas.SetActive(false);
            ApplyCursor();
        }

        private void OnEnable()
        {
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void Update()
        {
            if (!initialized)
                return;

            bool togglePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (Gamepad gamepad in Gamepad.all)
                togglePressed |= gamepad.startButton.wasPressedThisFrame;
            if (togglePressed && Application.isFocused)
                TogglePause();

            if (IsPaused && (menuDevicesChanged || gamepadConnected != (Gamepad.all.Count > 0)))
                ConfigureMenuDevices();

            // Starter Assets also changes cursor lock on application focus changes.
            ApplyCursor();
        }

        public void TogglePause()
        {
            if (!initialized || !isActiveAndEnabled)
                return;
            if (IsPaused)
                ResumeGameplay();
            else
                PauseGameplay();
        }

        private void PauseGameplay()
        {
            controllerWasEnabled = firstPersonController.enabled;
            armWasEnabled = armController.enabled;
            inputWasActive = playerInput.inputIsActive;
            cursorWasLocked = starterInputs.cursorLocked;
            cursorLookWasEnabled = starterInputs.cursorInputForLook;
            uiWasEnabled = uiInputModule.enabled;
            previousUIActions = uiInputModule.actionsAsset;

            // OnDisable already ends arm control, restores Look and preserves CurrentPose.
            armController.enabled = false;
            firstPersonController.enabled = false;
            enabledGameplayActions.Clear();
            if (playerInput.actions != null)
            {
                foreach (InputAction action in playerInput.actions)
                {
                    if (action.enabled)
                        enabledGameplayActions.Add(action);
                }
            }
            playerInput.DeactivateInput();
            if (playerInput.actions != null)
                playerInput.actions.Disable();
            starterInputs.cursorLocked = false;
            starterInputs.cursorInputForLook = false;
            ClearGameplayInput();

            IsPaused = true;
            Time.timeScale = 0f;
            pauseCanvas.SetActive(true);
            uiInputModule.enabled = false;
            uiInputModule.actionsAsset = menuActions;
            ConfigureMenuDevices();
        }

        private void ResumeGameplay()
        {
            if (uiInputModule != null)
                uiInputModule.enabled = false;
            if (menuActions != null)
                menuActions.Disable();
            if (eventSystem != null)
                eventSystem.SetSelectedGameObject(null);
            if (uiInputModule != null)
            {
                uiInputModule.actionsAsset = previousUIActions;
                uiInputModule.enabled = uiWasEnabled;
            }
            if (pauseCanvas != null)
                pauseCanvas.SetActive(false);

            if (starterInputs != null)
            {
                starterInputs.cursorLocked = cursorWasLocked;
                starterInputs.cursorInputForLook = cursorLookWasEnabled;
            }
            // Pause never disables PlayerInput itself. Do not revive it if another system did.
            bool canRestoreInput = playerInput != null && playerInput.isActiveAndEnabled
                && playerInput.gameObject.activeInHierarchy;
            if (canRestoreInput)
            {
                if (inputWasActive)
                    playerInput.ActivateInput();
                // ActivateInput enables a whole map. Restore each action's previous state instead.
                if (playerInput.actions != null)
                    playerInput.actions.Disable();
                foreach (InputAction action in enabledGameplayActions)
                {
                    if (action.actionMap != null && action.actionMap.asset != null)
                        action.Enable();
                }
            }
            if (canRestoreInput && starterInputs != null)
            {
                if (armController != null && armController.gameObject.activeInHierarchy)
                    armController.enabled = armWasEnabled;
                if (firstPersonController != null && firstPersonController.gameObject.activeInHierarchy)
                    firstPersonController.enabled = controllerWasEnabled;
            }
            ClearGameplayInput();
            enabledGameplayActions.Clear();
            IsPaused = false;
            Time.timeScale = 1f;
            ApplyCursor();
        }

        private void ClearGameplayInput()
        {
            if (starterInputs == null)
                return;

            starterInputs.MoveInput(Vector2.zero);
            starterInputs.LookInput(Vector2.zero);
            starterInputs.JumpInput(false);
            starterInputs.SprintInput(false);
        }

        private void ConfigureMenuDevices()
        {
            menuDevicesChanged = false;
            gamepadConnected = Gamepad.all.Count > 0;
            var devices = new List<InputDevice>();
            if (gamepadConnected)
            {
                foreach (Gamepad gamepad in Gamepad.all)
                    devices.Add(gamepad);
            }
            else
            {
                foreach (InputDevice device in InputSystem.devices)
                {
                    if (device is Mouse)
                        devices.Add(device);
                }
            }

            // Reset pointer/navigation state on hotplug; hidden mouse clicks must not affect selection.
            uiInputModule.enabled = false;
            menuActions.Disable();
            menuActions.devices = devices.ToArray();
            uiInputModule.enabled = true;
            eventSystem.SetSelectedGameObject(null);
            if (gamepadConnected && firstSelectedButton != null
                && firstSelectedButton.IsActive() && firstSelectedButton.IsInteractable())
                eventSystem.SetSelectedGameObject(firstSelectedButton.gameObject);
            ApplyCursor();
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (device is Gamepad || device is Mouse)
                menuDevicesChanged = true;
        }

        private void ApplyCursor()
        {
            bool useMouse = IsPaused && Gamepad.all.Count == 0;
            Cursor.lockState = useMouse ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = useMouse;
        }

        public void QuitGame()
        {
            Application.Quit();
        }

        public void SetSpanish()
        {
            StartCoroutine(SelectLocale("es-ES"));
        }

        public void SetEnglish()
        {
            StartCoroutine(SelectLocale("en"));
        }

        private IEnumerator SelectLocale(string localeCode)
        {
            var initialization = LocalizationSettings.InitializationOperation;
            if (!initialization.IsDone)
                yield return initialization;
            if (initialization.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("Localization initialization failed; cannot change locale.", this);
                yield break;
            }

            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(new LocaleIdentifier(localeCode));
            if (locale == null)
            {
                Debug.LogError($"The locale '{localeCode}' is not available in Localization Settings.", this);
                yield break;
            }

            LocalizationSettings.SelectedLocale = locale;
        }

        private void OnDisable()
        {
            // Cancel only the controller's pending locale-selection coroutines during teardown.
            StopAllCoroutines();
            InputSystem.onDeviceChange -= OnDeviceChange;

            // Scene teardown has no guaranteed destruction order. Never reactivate gameplay here.
            if (IsPaused)
                Time.timeScale = 1f;
            if (menuActions != null)
                menuActions.Disable();
            IsPaused = false;
            enabledGameplayActions.Clear();
            previousUIActions = null;
            menuDevicesChanged = false;
        }

        private void OnDestroy()
        {
            if (menuActions != null)
                Destroy(menuActions);
        }
    }
}
