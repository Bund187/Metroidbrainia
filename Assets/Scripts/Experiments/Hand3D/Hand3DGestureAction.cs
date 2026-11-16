using System;
using UnityEngine;
using UnityEngine.Events;

namespace Metroidbrainia.Hand3DExperiment
{
    [Flags] public enum Hand3DActionModes { Exploration=1, HandEdit=2, Both=3 }
    public enum Hand3DActionInput { Unspecified, Mouse, Gamepad }

    // Definitions are reusable assets; mutable execution state belongs to a per-request Run.
    public abstract class Hand3DGestureAction : ScriptableObject
    {
        [SerializeField] private Hand3DActionModes allowedModes=Hand3DActionModes.Both;
        [SerializeField] private bool requiresArmControl;
        [Tooltip("World actions additionally need valid contact, reach and visibility in CanBegin.")]
        [SerializeField] private bool affectsWorld;
        [SerializeField] private bool allowWorldInHandEdit;
        [Tooltip("Higher priorities compose later. Equal priorities use the configured list order.")]
        [SerializeField] private int priority;
        public int Priority=>priority;
        public bool RequiresArmControl=>requiresArmControl;
        public bool IsWorldAction=>affectsWorld;
        public bool IsAllowed(Hand3DActionContext context)=>
            (allowedModes&(context.Mode==Hand3DMode.HandEdit?Hand3DActionModes.HandEdit:Hand3DActionModes.Exploration))!=0
            && (!affectsWorld || context.Mode!=Hand3DMode.HandEdit || allowWorldInHandEdit)
            && (!(requiresArmControl || affectsWorld && context.Mode==Hand3DMode.Exploration) || context.ArmControlHeld);
        // Environmental subclasses must query Hand3DContactDetection here. No fake target is supplied.
        public virtual bool CanBegin(Hand3DActionContext context,out string reason){reason=null;return true;}
        public abstract Run Begin(Hand3DActionContext context);

        public abstract class Run
        {
            public virtual float IndexFlexWeight=>0f;
            public virtual string ProfileId=>null;
            public virtual float ProfileWeight=>0f;
            // True means complete. Instant actions return null from Begin and are never ticked.
            public abstract bool Tick(float deltaTime);
            public virtual void End(bool cancelled) { }
        }
    }

    public sealed class Hand3DActionContext
    {
        private readonly Hand3DPoseActions owner;
        private readonly UnityEvent configuredEvent;
        private bool eventInvoked;
        internal Hand3DActionContext(Hand3DPoseActions owner,HandPose gesture,UnityEvent configuredEvent,Hand3DActionInput input)
        {this.owner=owner;Gesture=gesture;this.configuredEvent=configuredEvent;Input=input;}
        public HandPose Gesture { get; }
        public Hand3DActionInput Input { get; }
        public Hand3DMode Mode=>owner.Movement!=null && owner.Movement.Modes!=null?owner.Movement.Modes.Mode:Hand3DMode.Exploration;
        public FlexedFingers Fingers=>owner.FingerState!=null?owner.FingerState.Current:FlexedFingers.None;
        public Hand3DInteractionCoordinator Interactions=>owner.Interactions;
        public MonoBehaviour InteractionTarget=>Interactions!=null?Interactions.Owner:null;
        public bool ArmControlHeld=>owner.InputController!=null && owner.InputController.IsArmModeActive;
        public Hand3DRuntimePoses Profiles=>owner.Rig!=null?owner.Rig.RuntimePoses:null;
        public bool UniversalActive=>owner.Rig!=null && owner.Rig.UniversalActive;
        public Camera ArmCamera=>owner.Movement!=null?owner.Movement.ArmCamera:null;
        public Transform PlayerRoot=>owner.InputController!=null?owner.InputController.PlayerRoot:null;
        public void InvokeConfiguredEvent(){if(eventInvoked)return;eventInvoked=true;configuredEvent?.Invoke();}
    }
}
