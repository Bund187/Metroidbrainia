using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Metroidbrainia.Hand3DExperiment
{
    [DisallowMultipleComponent]
    public sealed class Hand3DPoseActions : MonoBehaviour
    {
        [SerializeField] private Hand3DPoseRecognizer poseRecognizer;
        [Tooltip("Compatibility default used only when the gesture has no explicit binding below.")]
        [SerializeField, Min(0.01f)] private float pointClickDuration = 0.18f;
        [Tooltip("Blend towards each index joint's captured Flexed rotation; no fixed rotation axis.")]
        [SerializeField, Range(0f, 1f)] private float pointClickAmplitude = 0.18f;
        [Serializable] public sealed class Binding
        {
            public FlexedFingers gesture;
            public Hand3DGestureAction[] actions=Array.Empty<Hand3DGestureAction>();
            public UnityEvent onEvent=new UnityEvent();
            public bool invokeOriginalGestureEvent=true;
        }
        [Tooltip("Explicit combinations override the old Action field. An empty list deliberately means no action.")]
        [SerializeField] private Binding[] bindings=Array.Empty<Binding>();
        public struct VisualContribution
        {
            public string profileId;
            public float weight,indexFlexWeight;
        }
        private sealed class Active
        {
            public Hand3DGestureAction action;public Hand3DGestureAction.Run run;
            public Hand3DActionContext context;public Hand3DMode mode;public bool universal;
        }

        private Hand3DInteractionCoordinator interactions;
        private readonly List<Active> active=new List<Active>();
        private readonly List<VisualContribution> contributions=new List<VisualContribution>();
        private readonly Dictionary<PoseAction,Hand3DGestureAction> compatibility=new Dictionary<PoseAction,Hand3DGestureAction>();
        private Hand3DModeController modes;
        private int generation;
        private bool dispatching;
        public float PointClickWeight { get; private set; }
        public string LastActionError { get; private set; }
        public IReadOnlyList<VisualContribution> VisualContributions=>contributions;
        public Hand3DArmMovement Movement { get; private set; }
        public Hand3DRig Rig { get; private set; }
        public Hand3DFingerState FingerState { get; private set; }
        public Hand3DInputController InputController { get; private set; }
        public Hand3DInteractionCoordinator Interactions=>interactions;
        public bool IsRunning=>active.Count>0;

        private void OnEnable()
        {
            interactions = GetComponent<Hand3DInteractionCoordinator>();
            Movement=GetComponent<Hand3DArmMovement>();Rig=GetComponent<Hand3DRig>();
            FingerState=GetComponent<Hand3DFingerState>();InputController=GetComponent<Hand3DInputController>();
            modes=Movement!=null?Movement.Modes:null;
            if(modes==null)modes=GetComponent<Hand3DModeController>();
            if (poseRecognizer == null)
                poseRecognizer = GetComponent<Hand3DPoseRecognizer>();
            if (poseRecognizer != null)
                poseRecognizer.PoseChanged += CancelAction;
            if(modes!=null)modes.ModeChanged+=CancelAction;
            if(interactions!=null)interactions.ArmControlEnded+=ArmControlEnded;
            // Compatibility wiring only: gesture recognition and execution contain no named-gesture cases.
            var click=ScriptableObject.CreateInstance<Hand3DPointClickAction>();click.ConfigureCompatibility(pointClickDuration,pointClickAmplitude);
            compatibility[PoseAction.PointClick]=click;
            compatibility[PoseAction.EventOnly]=ScriptableObject.CreateInstance<Hand3DEventAction>();
            CancelAction();
        }

        public bool TryExecute()=>TryExecute(Hand3DActionInput.Unspecified);
        public bool TryExecute(Hand3DActionInput input)
        {
            if (!isActiveAndEnabled || !ExperimentScope.IsAllowed(this) || Time.timeScale <= 0f || IsRunning || dispatching
                || interactions != null && !interactions.CanExecuteGestureAction)
                return false;
            HandPose pose = poseRecognizer != null ? poseRecognizer.CurrentPose : null;
            if(pose==null)return false;
            LastActionError=null;
            Binding binding=null;
            foreach(var candidate in bindings)if(candidate!=null && candidate.gesture==pose.Combination)
            {if(binding!=null){LastActionError="Duplicate action binding for "+pose.Combination;Debug.LogError(LastActionError,this);return false;}binding=candidate;}
            Hand3DGestureAction[] requested;
            if(binding!=null)requested=binding.actions??Array.Empty<Hand3DGestureAction>();
            else if(compatibility.TryGetValue(pose.Action,out var old))requested=new[]{old};
            else return false;
            var context=new Hand3DActionContext(this,pose,binding?.onEvent,input);
            var selected=new List<Hand3DGestureAction>();
            foreach(var action in requested)
            {
                if(action==null || !action.IsAllowed(context))continue;
                try{if(!action.CanBegin(context,out string reason)){LastActionError=reason;continue;}}
                catch(Exception error){LastActionError=error.Message;Debug.LogException(error,this);return false;}
                selected.Add(action);
            }
            if(selected.Count==0){if(LastActionError!=null)Debug.LogWarning(LastActionError,this);return false;}
            // Stable insertion sort: higher priority writes later, equal priority follows Inspector order.
            for(int i=1;i<selected.Count;i++){var next=selected[i];int j=i-1;while(j>=0 && selected[j].Priority>next.Priority){selected[j+1]=selected[j];j--;}selected[j+1]=next;}
            dispatching=true;int token=generation;
            try
            {
                foreach(var action in selected)
                {
                    var run=action.Begin(context);
                    if(token!=generation){run?.End(true);return true;}
                    if(run!=null)active.Add(new Active{action=action,run=run,context=context,mode=context.Mode,universal=context.UniversalActive});
                }
                RebuildContributions();
                if(binding==null || binding.invokeOriginalGestureEvent)pose.OnAction?.Invoke();
                return true;
            }
            catch(Exception error){CancelAction();LastActionError=error.Message;Debug.LogException(error,this);return false;}
            finally{dispatching=false;}
        }

        private void Update()=>Tick(Time.deltaTime);
        private void Tick(float dt)
        {
            if(Time.timeScale<=0f){CancelAction();return;}
            if(dt<=0f || active.Count==0)return;
            if(interactions!=null && !interactions.CanExecuteGestureAction || Rig!=null && !Rig.isActiveAndEnabled || Movement!=null && !Movement.isActiveAndEnabled || poseRecognizer!=null && !poseRecognizer.isActiveAndEnabled){CancelAction();return;}
            try
            {
                for(int i=0;i<active.Count;)
                {
                    var item=active[i];
                    if(!item.action.IsAllowed(item.context) || item.context.Mode!=item.mode || item.context.UniversalActive!=item.universal){CancelAction();return;}
                    int token=generation;bool complete=item.run.Tick(dt);
                    if(token!=generation)return;
                    if(complete){active.RemoveAt(i);item.run.End(false);}else i++;
                }
                RebuildContributions();
            }
            catch(Exception error){CancelAction();LastActionError=error.Message;Debug.LogException(error,this);}
        }

        private void RebuildContributions()
        {
            contributions.Clear();PointClickWeight=0f;
            foreach(var item in active)
            {
                float index=item.run.IndexFlexWeight,weight=item.run.ProfileWeight;
                if(float.IsNaN(index) || float.IsInfinity(index) || float.IsNaN(weight) || float.IsInfinity(weight))throw new InvalidOperationException("Non-finite visual weight from action "+item.action.name);
                index=Mathf.Clamp01(index);weight=Mathf.Clamp01(weight);
                PointClickWeight=1f-(1f-PointClickWeight)*(1f-index);
                contributions.Add(new VisualContribution{profileId=item.run.ProfileId,weight=weight,indexFlexWeight=index});
            }
        }

        public void CancelAction()
        {
            generation++;
            var ending=active.ToArray();active.Clear();contributions.Clear();PointClickWeight=0f;
            foreach(var item in ending)try{item.run.End(true);}catch(Exception error){Debug.LogException(error,this);}
        }

        private void ArmControlEnded(){foreach(var item in active)if(item.action.RequiresArmControl || item.action.IsWorldAction){CancelAction();return;}}
        private void OnApplicationFocus(bool focused){if(!focused)CancelAction();}
        private void OnApplicationPause(bool paused){if(paused)CancelAction();}

        private void OnDisable()
        {
            if (poseRecognizer != null)
                poseRecognizer.PoseChanged -= CancelAction;
            if(modes!=null)modes.ModeChanged-=CancelAction;
            if(interactions!=null)interactions.ArmControlEnded-=ArmControlEnded;
            CancelAction();
            foreach(var definition in compatibility.Values){if(Application.isPlaying)Destroy(definition);else DestroyImmediate(definition);}
            compatibility.Clear();
        }
    }
}
