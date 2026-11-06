using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Metroidbrainia
{
    public enum ArmPose
    {
        Rest,
        Point,
        OK,
        Grab
    }

    // Process mode changes before FirstPersonController.Update and its camera LateUpdate.
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class FirstPersonArmController : MonoBehaviour
    {
        [SerializeField] private PlayerInput playerInput;
        [SerializeField] private StarterAssetsInputs starterInputs;
        [SerializeField] private InputActionAsset armControls;
        [SerializeField] private ArmView armView;
        [Header("Automatic hand interaction")]
        [SerializeField] private Camera interactionCamera;
        [SerializeField, Min(0.01f)] private float interactionDistance = 2f;
        [Tooltip("Include physical obstacles as well as interactables. Exclude the player's layer if needed.")]
        [SerializeField] private LayerMask interactionLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0f)] private float mouseSensitivity = 1f;
        [SerializeField, Min(0f)] private float stickSpeed = 900f;
        [SerializeField] private UnityEvent onPointAction = new UnityEvent();
        [SerializeField] private UnityEvent onOKAction = new UnityEvent();
        [SerializeField] private UnityEvent onGrabAction = new UnityEvent();

        private InputActionAsset runtimeControls;
        private InputAction armMode;
        private InputAction armMove;
        private InputAction armAction;
        private InputAction selectPoint;
        private InputAction selectOK;
        private InputAction selectGrab;
        private InputAction selectRest;
        private InputAction look;
        private InputAction jump;
        private InputAction sprint;
        private bool lookWasEnabled;
        private bool initialized;
        private bool hasFocus = true;
        private bool paused;
        private bool waitForRelease;
        private IHandInteractable activeInteractable;
        private IHandInteractable hoveredInteractable;
        private Collider hoveredCollider;
        private bool freeHandReturning;

        private struct SavedBinding
        {
            public InputAction Action;
            public Guid Id;
            public InputBinding Binding;
        }

        private readonly List<SavedBinding> blockedBindings = new List<SavedBinding>();

        public ArmPose CurrentPose { get; private set; } = ArmPose.Rest;
        public bool IsArmModeActive { get; private set; }

        private void Reset()
        {
            playerInput = GetComponent<PlayerInput>();
            starterInputs = GetComponent<StarterAssetsInputs>();
        }

        private void Start()
        {
            if (playerInput == null || starterInputs == null || armControls == null || armView == null)
            {
                Debug.LogError("Assign Player Input, Starter Inputs, Arm Controls and Arm View.", this);
                enabled = false;
                return;
            }

            if (!armView.Initialize())
            {
                enabled = false;
                return;
            }

            // Own the arm actions; Starter Assets actions are accessed through its PlayerInput.
            runtimeControls = Instantiate(armControls);
            armMode = runtimeControls.FindAction("Arm/ArmMode");
            armMove = runtimeControls.FindAction("Arm/ArmMove");
            armAction = runtimeControls.FindAction("Arm/ArmAction");
            selectPoint = runtimeControls.FindAction("Arm/SelectPoint");
            selectOK = runtimeControls.FindAction("Arm/SelectOK");
            selectGrab = runtimeControls.FindAction("Arm/SelectGrab");
            selectRest = runtimeControls.FindAction("Arm/SelectRest");
            look = playerInput.actions?.FindAction("Player/Look");
            jump = playerInput.actions?.FindAction("Player/Jump");
            sprint = playerInput.actions?.FindAction("Player/Sprint");
            if (armMode == null || armMove == null || armAction == null
                || selectPoint == null || selectOK == null || selectGrab == null || selectRest == null
                || look == null || jump == null || sprint == null)
            {
                Debug.LogError("Missing arm actions or Starter Assets Player/Look, Jump, Sprint actions.", this);
                enabled = false;
                return;
            }

            initialized = true;
            if (interactionCamera == null)
                interactionCamera = Camera.main;
            ActivateInput();
            armView.PlayPose(CurrentPose);
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
            BlockBinding(jump, "<Gamepad>/buttonSouth");
            BlockBinding(sprint, "<Gamepad>/leftTrigger");
            starterInputs.JumpInput(false);
            starterInputs.SprintInput(false);
            runtimeControls.Enable();
        }

        private void BlockBinding(InputAction action, string path)
        {
            for (int i = 0; i < action.bindings.Count; i++)
            {
                InputBinding binding = action.bindings[i];
                if (binding.isComposite || binding.isPartOfComposite
                    || !string.Equals(binding.path, path, StringComparison.OrdinalIgnoreCase))
                    continue;

                blockedBindings.Add(new SavedBinding { Action = action, Id = binding.id, Binding = binding });
                // Keep any existing processors/interactions, including null overrides.
                binding.overridePath = string.Empty;
                action.ApplyBindingOverride(i, binding);
            }
        }

        private void RestoreBindings()
        {
            foreach (SavedBinding saved in blockedBindings)
            {
                for (int i = 0; i < saved.Action.bindings.Count; i++)
                {
                    if (saved.Action.bindings[i].id != saved.Id)
                        continue;
                    saved.Action.ApplyBindingOverride(i, saved.Binding);
                    break;
                }
            }
            blockedBindings.Clear();
        }

        private void Update()
        {
            if (!initialized || !hasFocus || paused)
                return;

            if (!playerInput.isActiveAndEnabled || !playerInput.inputIsActive)
            {
                EndArmMode();
                waitForRelease = true;
                return;
            }

            bool modeHeld = armMode.IsPressed();
            if (waitForRelease)
            {
                waitForRelease = modeHeld;
                modeHeld = false;
            }

            // Determine mode first so same-frame selection follows the correct mode rule.
            if (modeHeld && !IsArmModeActive)
                BeginArmMode();
            else if (!modeHeld)
                EndArmMode();

            // Deterministic priority if several pose buttons are pressed in one frame.
            if (selectRest.WasPressedThisFrame())
                SelectPose(ArmPose.Rest);
            else if (selectPoint.WasPressedThisFrame())
                SelectPose(ArmPose.Point);
            else if (selectOK.WasPressedThisFrame())
                SelectPose(ArmPose.OK);
            else if (selectGrab.WasPressedThisFrame())
                SelectPose(ArmPose.Grab);

            if (!IsArmModeActive)
                return;

            look.Disable();
            starterInputs.LookInput(Vector2.zero);
            Vector2 movement = armMove.ReadValue<Vector2>();
            bool usingMouse = armMove.activeControl?.device is Mouse;
            Vector2 displacement = movement * (usingMouse ? mouseSensitivity : stickSpeed * Time.deltaTime);
            if (activeInteractable != null)
            {
                if (activeInteractable is IFreeHandInteractable freeInteraction)
                {
                    // A free-hand hold owns no movement input and keeps working with its world collider hidden.
                    if (!IsInteractableAlive(activeInteractable) || !freeInteraction.IsInteractionActive)
                        EndHandInteraction();
                    else
                        armView.MoveHand(displacement);
                    return;
                }
                if (!IsInteractionVisible(activeInteractable)
                    || !activeInteractable.UpdateInteraction(interactionCamera, armView.GetScreenDisplacement(displacement)))
                    EndHandInteraction();
                return;
            }

            if (freeHandReturning)
            {
                if (displacement.sqrMagnitude < 0.000001f)
                {
                    // A held ArmMode must not immediately interrupt the completed interaction's return.
                    if (armAction.WasPressedThisFrame())
                        PerformArmAction();
                    return;
                }
                freeHandReturning = false;
                armView.BeginControl();
            }
            armView.MoveHand(displacement);
            DetectHandInteraction();
            if (activeInteractable != null)
                return;

            // Selection is processed first; only an actual action press emits a logical event.
            if (armAction.WasPressedThisFrame())
                PerformArmAction();
        }

        public void SelectPose(ArmPose requestedPose)
        {
            if (!initialized || !isActiveAndEnabled)
                return;
            if (requestedPose < ArmPose.Rest || requestedPose > ArmPose.Grab)
                return;
            if (IsArmModeActive && requestedPose == CurrentPose)
                return;

            CurrentPose = requestedPose;
            if (activeInteractable == null)
                armView.PlayPose(CurrentPose);
        }

        private static bool IsInteractableAlive(IHandInteractable interactable)
        {
            return interactable is MonoBehaviour component && component != null && component.isActiveAndEnabled;
        }

        private void DetectHandInteraction()
        {
            IHandInteractable candidate = null;
            if (TryRaycastHand(out RaycastHit hit))
                candidate = hit.collider.GetComponentInParent<IHandInteractable>();

            if (!IsInteractableAlive(candidate))
                candidate = null;
            if (hoveredInteractable != candidate || hoveredCollider != hit.collider)
            {
                EndHover();
                hoveredInteractable = candidate;
                hoveredCollider = hit.collider;
            }
            if (candidate == null || !candidate.TryBeginInteraction(interactionCamera, hit.collider))
                return;

            if (candidate is IFreeHandInteractable freeInteraction)
            {
                if (!armView.TryPlayInteractionVisual(freeInteraction.HandVisual))
                {
                    candidate.EndInteraction();
                    armView.PlayPose(CurrentPose);
                    return;
                }
                activeInteractable = candidate;
                return;
            }

            if (!armView.FollowWorldPoint(candidate.InteractionPoint, interactionCamera))
            {
                candidate.EndInteraction();
                return;
            }
            activeInteractable = candidate;
            // Retain contact until the ray leaves, including after a short captured feedback animation.
            armView.PlayPose(ArmPose.Grab);
        }

        private bool IsInteractionVisible(IHandInteractable interactable)
        {
            if (!IsInteractableAlive(interactable) || interactionCamera == null || interactable.InteractionPoint == null)
                return false;

            Vector3 screenPoint = interactionCamera.WorldToScreenPoint(interactable.InteractionPoint.position);
            if (screenPoint.z <= 0f || !interactionCamera.pixelRect.Contains(screenPoint))
                return false;

            Vector3 offset = interactable.InteractionPoint.position - interactionCamera.transform.position;
            if (offset.magnitude > interactionDistance)
                return false;

            Physics.SyncTransforms();
            Ray ray = interactionCamera.ScreenPointToRay(screenPoint);
            if (!Physics.Raycast(ray, out RaycastHit hit, interactionDistance, interactionLayers, QueryTriggerInteraction.Collide))
                return false;
            return hit.collider.GetComponentInParent<IHandInteractable>() == interactable;
        }

        private void EndHover()
        {
            if (IsInteractableAlive(hoveredInteractable))
                hoveredInteractable.EndInteraction();
            hoveredInteractable = null;
            hoveredCollider = null;
        }

        private void EndHandInteraction()
        {
            IHandInteractable previous = activeInteractable;
            activeInteractable = null;
            if (IsInteractableAlive(previous))
                previous.EndInteraction();
            if (armView != null)
            {
                if (!(previous is IFreeHandInteractable) || !IsArmModeActive)
                    armView.EndWorldFollow();
                armView.PlayPose(CurrentPose);
            }
            freeHandReturning = !(previous is IFreeHandInteractable) || !IsArmModeActive;
        }

        private void BeginArmMode()
        {
            freeHandReturning = false;
            lookWasEnabled = look.enabled;
            look.Disable();
            starterInputs.LookInput(Vector2.zero);
            armView.BeginControl();
            IsArmModeActive = true;
        }

        private void EndArmMode()
        {
            if (!IsArmModeActive)
                return;

            IsArmModeActive = false;
            if (activeInteractable != null)
                EndHandInteraction();
            EndHover();
            if (lookWasEnabled)
                look.Enable();

            // Never replay a stored mouse delta from arm mode.
            starterInputs.LookInput(Vector2.zero);
            if (armView != null)
                armView.EndControl();
        }

        public void PerformArmAction()
        {
            if (!initialized || !isActiveAndEnabled || !IsArmModeActive
                || activeInteractable != null || CurrentPose == ArmPose.Rest)
                return;

            armView.PlayPose(CurrentPose);
            if (TryRaycastHand(out RaycastHit hit))
            {
                IHandActionInteractable target = hit.collider.GetComponentInParent<IHandActionInteractable>();
                if (target is MonoBehaviour component && component != null && component.isActiveAndEnabled)
                    target.TryPerformHandAction(CurrentPose, interactionCamera, hit);
            }
            switch (CurrentPose)
            {
                case ArmPose.Point: onPointAction.Invoke(); break;
                case ArmPose.OK: onOKAction.Invoke(); break;
                case ArmPose.Grab: onGrabAction.Invoke(); break;
            }
        }

        private bool TryRaycastHand(out RaycastHit hit)
        {
            hit = default;
            if (interactionCamera == null)
                interactionCamera = Camera.main;
            if (interactionCamera == null)
                return false;

            // Procedurally moved colliders have no Rigidbody; query their latest transforms.
            Physics.SyncTransforms();
            Ray ray = interactionCamera.ScreenPointToRay(GetHandScreenPosition());
            return Physics.Raycast(ray, out hit, interactionDistance, interactionLayers, QueryTriggerInteraction.Collide);
        }

        public Vector2 GetHandScreenPosition()
        {
            return armView.GetHandScreenPosition();
        }

        private void OnApplicationFocus(bool focused)
        {
            hasFocus = focused;
            if (!focused)
            {
                EndArmMode();
                waitForRelease = true;
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            paused = isPaused;
            if (paused)
            {
                EndArmMode();
                waitForRelease = true;
            }
        }

        private void OnDisable()
        {
            EndArmMode();
            if (runtimeControls != null)
                runtimeControls.Disable();
            RestoreBindings();
            if (starterInputs != null && initialized)
            {
                starterInputs.JumpInput(false);
                starterInputs.SprintInput(false);
            }
        }

        private void OnDestroy()
        {
            if (runtimeControls != null)
                Destroy(runtimeControls);
        }
    }
}
