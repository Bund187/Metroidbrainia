using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Metroidbrainia.Hand3DExperiment
{
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class Hand3DInputController : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private StarterAssetsInputs starterInputs;
        [SerializeField] private Hand3DFingerState fingerState;
        [SerializeField] private Hand3DRig rig;
        [SerializeField] private Hand3DArmMovement armMovement;
        [SerializeField] private Hand3DPoseActions poseActions;
        [Tooltip("Integrated input per mouse pixel; mouse delta is not multiplied by delta time.")]
        [SerializeField, Min(0f)] private float mouseSensitivity = 1f;
        [Tooltip("Multiplier for stick input, integrated with delta time.")]
        [SerializeField, Min(0f)] private float gamepadSensitivity = 1f;

        private InputActionMap controls;
        private InputAction armMode;
        private InputAction armMove;
        private InputAction action;
        private InputAction toggleMode;
        private InputAction inspect;
        private bool gameplayBlocked;
        private FirstPersonController characterController;
        private bool characterWasEnabled;
        private bool characterSuspended;
        private readonly InputAction[] fingerButtons = new InputAction[5];
        private readonly InputAction[] blockedGameplay = new InputAction[4];
        private readonly bool[] gameplayWasEnabled = new bool[4];
        private readonly List<Guid> originalEnabledActions = new List<Guid>();
        private InputActionAsset originalPlayerActions;
        private InputActionAsset runtimePlayerActions;
        private string originalMapName;
        private bool cursorLookWasEnabled;
        private bool initialized;
        private bool hasFocus = true;
        private bool applicationPaused;
        private bool waitForRelease;

        public bool IsArmModeActive { get; private set; }

        private void Start()
        {
            if (!ExperimentScope.IsAllowed(this) || playerInput == null || starterInputs == null || fingerState == null
                || rig == null || armMovement == null || poseActions == null || playerInput.actions == null
                || playerInput.gameObject.scene != gameObject.scene || starterInputs.gameObject.scene != gameObject.scene
                || fingerState.gameObject != gameObject || rig.gameObject != gameObject
                || armMovement.gameObject != gameObject || poseActions.gameObject != gameObject)
            {
                Debug.LogError("Configure the experimental controllers together in TestArmScene and assign that scene's PlayerInput and StarterAssetsInputs.", this);
                enabled = false;
                return;
            }
            if (!rig.IsReady || !armMovement.IsReady)
            {
                Debug.LogError("Capture the finger rotations and configure the experimental arm chain before enabling input.", this);
                enabled = false;
                return;
            }
            controls = new InputActionMap("TestHand3D");
            armMode = controls.AddAction("ArmMode", InputActionType.Button);
            armMode.AddBinding("<Gamepad>/rightTrigger");
            armMode.AddBinding("<Mouse>/rightButton");
            armMove = controls.AddAction("ArmMove", InputActionType.Value);
            armMove.expectedControlType = "Vector2";
            armMove.AddBinding("<Gamepad>/rightStick");
            armMove.AddBinding("<Mouse>/delta");
            toggleMode = controls.AddAction("ToggleHandEdit", InputActionType.Button);
            toggleMode.AddBinding("<Gamepad>/rightShoulder");
            toggleMode.AddBinding("<Keyboard>/leftShift");
            toggleMode.AddBinding("<Keyboard>/rightShift");
            inspect = controls.AddAction("InspectHand", InputActionType.Value);
            inspect.AddBinding("<Gamepad>/leftStick/x");
            inspect.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            action = controls.AddAction("Action", InputActionType.Button);
            action.AddBinding("<Gamepad>/leftTrigger");
            action.AddBinding("<Mouse>/leftButton");
            AddFinger(Finger.Thumb, "<Gamepad>/rightStickPress", "<Keyboard>/e");
            AddFinger(Finger.Index, "<Gamepad>/dpad/up", "<Keyboard>/r");
            AddFinger(Finger.Middle, "<Gamepad>/dpad/right", "<Keyboard>/t");
            AddFinger(Finger.Ring, "<Gamepad>/dpad/down", "<Keyboard>/f");
            AddFinger(Finger.Little, "<Gamepad>/dpad/left", "<Keyboard>/g");
            initialized = true;
            ActivateInput();
        }

        private void AddFinger(Finger finger, string gamepad, string keyboard)
        {
            InputAction input = controls.AddAction("Toggle" + finger, InputActionType.Button);
            input.AddBinding(gamepad);
            input.AddBinding(keyboard);
            fingerButtons[(int)finger] = input;
        }

        private void OnEnable()
        {
            if (initialized)
            {
                waitForRelease = true;
                ActivateInput();
            }
        }

        private void ActivateInput()
        {
            if (playerInput == null || playerInput.actions == null)
                return;
            originalPlayerActions = playerInput.actions;
            originalMapName = playerInput.currentActionMap != null ? playerInput.currentActionMap.name : null;
            originalEnabledActions.Clear();
            foreach (InputAction input in originalPlayerActions)
                if (input.enabled) originalEnabledActions.Add(input.id);
            // All gameplay action changes, including the LT/Sprint override, stay on a private scene-instance clone.
            runtimePlayerActions = Instantiate(originalPlayerActions);
            playerInput.actions = runtimePlayerActions;
            if (playerInput.inputIsActive && !string.IsNullOrEmpty(originalMapName))
                playerInput.SwitchCurrentActionMap(originalMapName);
            RestoreEnabledActions(runtimePlayerActions);
            string[] names = { "Player/Look", "Player/Move", "Player/Jump", "Player/Sprint" };
            for (int i = 0; i < blockedGameplay.Length; i++)
            {
                blockedGameplay[i] = runtimePlayerActions.FindAction(names[i]);
                if (blockedGameplay[i] == null)
                {
                    Debug.LogError("The test player needs Starter Assets Player/Look, Move, Jump and Sprint actions.", this);
                    enabled = false;
                    return;
                }
            }
            InputAction sprint = blockedGameplay[3];
            for (int i = 0; i < sprint.bindings.Count; i++)
                if (string.Equals(sprint.bindings[i].path, "<Gamepad>/leftTrigger", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrEmpty(sprint.bindings[i].path)
                        && sprint.bindings[i].path.IndexOf("shift", StringComparison.OrdinalIgnoreCase) >= 0)
                    sprint.ApplyBindingOverride(i, string.Empty);
            starterInputs.SprintInput(false);
            controls.Enable();
            characterController = playerInput.GetComponent<FirstPersonController>();
        }

        private void RestoreEnabledActions(InputActionAsset asset)
        {
            asset.Disable();
            if (playerInput != null && playerInput.inputIsActive)
                foreach (Guid id in originalEnabledActions)
                    asset.FindAction(id)?.Enable();
        }

        private void Update()
        {
            if (!initialized || !hasFocus || applicationPaused || Time.deltaTime <= 0f
                || playerInput == null || !playerInput.isActiveAndEnabled || !playerInput.inputIsActive)
            {
                EndArmMode();
                return;
            }
            if (toggleMode.WasPressedThisFrame())
            {
                EndArmMode();
                armMovement.ToggleMode();
                waitForRelease = armMode.IsPressed();
            }
            bool editing = armMovement.Modes.IsEditing;
            bool rawHeld = armMode.IsPressed();
            bool held = rawHeld && !editing && !armMovement.Modes.IsTransitioning;
            if (waitForRelease)
            {
                waitForRelease = rawHeld;
                held = false;
            }
            if (held && !IsArmModeActive) BeginArmMode();
            else if (!held) EndArmMode();
            SetGameplayBlocked(editing || IsArmModeActive);
            SuspendCharacter(editing);
            if (editing)
                armMovement.Modes.Inspect(inspect.ReadValue<float>() * Time.deltaTime *
                    (inspect.activeControl?.device is Gamepad ? gamepadSensitivity : 1f));
            if (gameplayBlocked) ClearPlayerInput();

            for (int i = 0; i < fingerButtons.Length; i++)
                if (fingerButtons[i].WasPressedThisFrame())
                    fingerState.Toggle((Finger)i);
            if (IsArmModeActive)
            {
                ClearPlayerInput();
                Vector2 movement = armMove.ReadValue<Vector2>();
                bool mouse = armMove.activeControl != null && armMove.activeControl.device is Mouse;
                armMovement.MoveHand(movement * (mouse ? mouseSensitivity : gamepadSensitivity * Time.deltaTime), mouse);
            }
            // Deliberately independent of ArmMode. R3 remains a separate button from right-stick axes.
            if (action.WasPressedThisFrame())
                poseActions.TryExecute();
        }

        private void BeginArmMode()
        {
            SetGameplayBlocked(true);
            armMovement.BeginControl();
            IsArmModeActive = true;
        }

        private void SetGameplayBlocked(bool blocked)
        {
            if (gameplayBlocked == blocked) return;
            gameplayBlocked = blocked;
            if (!blocked)
            {
                if (starterInputs != null) starterInputs.cursorInputForLook = cursorLookWasEnabled;
                for (int i = 0; i < blockedGameplay.Length; i++)
                    if (gameplayWasEnabled[i] && blockedGameplay[i] != null && playerInput != null
                        && playerInput.isActiveAndEnabled && playerInput.inputIsActive) blockedGameplay[i].Enable();
                ClearPlayerInput();
                return;
            }
            cursorLookWasEnabled = starterInputs.cursorInputForLook;
            starterInputs.cursorInputForLook = false;
            for (int i = 0; i < blockedGameplay.Length; i++)
            {
                gameplayWasEnabled[i] = blockedGameplay[i].enabled;
                blockedGameplay[i].Disable();
            }
            ClearPlayerInput();
        }

        private void SuspendCharacter(bool suspended)
        {
            if (characterController == null || characterSuspended == suspended) return;
            characterSuspended = suspended;
            if (suspended)
            {
                characterWasEnabled = characterController.enabled;
                characterController.enabled = false;
            }
            else characterController.enabled = characterWasEnabled;
        }

        private void EndArmMode()
        {
            if (!IsArmModeActive)
                return;
            IsArmModeActive = false;
            ClearPlayerInput();
            if (armMovement != null) armMovement.EndControl();
        }

        private void ClearPlayerInput()
        {
            if (starterInputs == null) return;
            starterInputs.LookInput(Vector2.zero);
            starterInputs.MoveInput(Vector2.zero);
            starterInputs.JumpInput(false);
            starterInputs.SprintInput(false);
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused) { EndArmMode(); waitForRelease = true; }
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused) { EndArmMode(); waitForRelease = true; }
        }

        private void OnDisable()
        {
            EndArmMode();
            SetGameplayBlocked(false);
            SuspendCharacter(false);
            controls?.Disable();
            if (runtimePlayerActions == null)
                return;
            runtimePlayerActions.Disable();
            // During scene teardown never reactivate a player that is already disabled or destroyed.
            if (playerInput != null && playerInput.isActiveAndEnabled && playerInput.gameObject.activeInHierarchy
                && playerInput.actions == runtimePlayerActions && originalPlayerActions != null)
            {
                playerInput.actions = originalPlayerActions;
                if (playerInput.inputIsActive && !string.IsNullOrEmpty(originalMapName)) playerInput.SwitchCurrentActionMap(originalMapName);
                RestoreEnabledActions(originalPlayerActions);
            }
            Destroy(runtimePlayerActions);
            runtimePlayerActions = null;
        }

        private void OnDestroy() => controls?.Dispose();
    }
}
