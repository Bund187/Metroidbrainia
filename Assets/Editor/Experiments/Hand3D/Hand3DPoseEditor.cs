using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using P = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;
using S = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfileSet;

namespace Metroidbrainia.Hand3DExperiment.Editor
{
    public sealed class Hand3DPoseEditor : EditorWindow
    {
        private enum ContextView { Exploration, HandEdit, Interaction, Action }
        [SerializeField] private Hand3DRig rig;
        [SerializeField] private Hand3DPoseLibrary library;
        [SerializeField] private string selectedId, referenceId, saveName="HandPoses", newName="New pose", newId="";
        [SerializeField] private ContextView context;
        [SerializeField] private Hand3DPosePreview.SelectionMask mask=Hand3DPosePreview.SelectionMask.FullBody;
        [SerializeField] private P.Channels channels=P.Channels.Rotation;
        [SerializeField] private bool segmentPositions, jointDetails;
        [SerializeField] private bool showJoints, showValues;
        [SerializeField] private string[] custom=Array.Empty<string>(), companions=Array.Empty<string>();
        private Hand3DPosePreview preview;
        private Vector2 scroll;
        private string notice, fingerprint;
        [SerializeField] private string savedSnapshot;
        [SerializeField] private bool wasDirty;
        private Transform extraBone;
        private S.Role extraRole=S.Role.Twist;
        private Finger extraFinger;
        [SerializeField] private Hand3DRuntimePoses runtimeTarget;
        [SerializeField] private string[] publishIds=Array.Empty<string>();
        [SerializeField] private bool initialCopy;
        private string publicationReview;
        public Hand3DPoseLibrary Library => library;
        public Hand3DPosePreview Preview => preview;

        [MenuItem("Tools/Metroidbrainia/Hand 3D/Pose Editor")]
        public static void Open()=>GetWindow<Hand3DPoseEditor>("Hand 3D Pose Editor");
        private void OnEnable()
        {
            Undo.undoRedoPerformed+=OnUndoRedo;AssemblyReloadEvents.beforeAssemblyReload+=Restore;
            EditorApplication.playModeStateChanged+=OnPlayMode;saveChangesMessage="Save the experimental pose library before closing?";
            fingerprint=library!=null?JsonUtility.ToJson(library.Data):null;
        }
        private void OnDisable(){Restore();Undo.undoRedoPerformed-=OnUndoRedo;AssemblyReloadEvents.beforeAssemblyReload-=Restore;EditorApplication.playModeStateChanged-=OnPlayMode;}
        private void OnDestroy(){if(library!=null && !EditorUtility.IsPersistent(library))DestroyImmediate(library);}
        private void OnPlayMode(PlayModeStateChange state){if(state==PlayModeStateChange.ExitingEditMode)Restore();}
        private void OnUndoRedo()
        {
            string next=library!=null?JsonUtility.ToJson(library.Data):null;
            if(next!=fingerprint)Restore();fingerprint=next;hasUnsavedChanges=library!=null && EditorUtility.IsDirty(library);Repaint();
        }
        public void Bind(Hand3DRig source,Hand3DPoseLibrary value)
        {
            Hand3DPoseLibrary.ValidateBinding(source,value.Data);Restore();
            if(library!=null && library!=value && !EditorUtility.IsPersistent(library))DestroyImmediate(library);
            rig=source;library=value;runtimeTarget=source.RuntimePoses;publishIds=Array.Empty<string>();publicationReview=null;selectedId=null;referenceId=null;companions=Array.Empty<string>();fingerprint=JsonUtility.ToJson(value.Data);savedSnapshot=EditorJsonUtility.ToJson(value);wasDirty=EditorUtility.IsDirty(value);
        }
        public void SelectProfile(string id)
        {
            if(!library.Data.profiles.Any(p=>p.id==id))throw new InvalidOperationException("Unknown profile.");
            if(id!=selectedId)Restore();selectedId=id;if(referenceId==id)referenceId=null;companions=companions.Where(key=>key!=selectedId && (library.Data.profiles.Single(p=>p.id==key).contexts&CurrentContext)!=0).ToArray();
        }
        public void BeginPreview(bool apply=true)
        {
            Restore();preview=Hand3DPosePreview.Open(rig,library,selectedId,referenceId,companions,apply,CurrentContext);
        }
        public void Capture()
        {
            if(preview==null)throw new InvalidOperationException("Open Preview or Begin Manual Pose first; edit the disposable copy.");
            preview.Capture(Hand3DPosePreview.SelectPaths(library.Data,mask,custom),channels,segmentPositions);Changed();
        }
        public void Restore(){if(preview!=null)preview.Restore();preview=null;}
        private void Changed(){publicationReview=null;fingerprint=JsonUtility.ToJson(library.Data);hasUnsavedChanges=true;Repaint();}
        private void Run(Action operation){try{operation();notice=null;}catch(Exception error){notice=error.Message;}}
        public override void SaveChanges()
        {
            if(library==null){base.SaveChanges();return;}
            try
            {
                if(Path.GetFileName(saveName)!=saveName || string.IsNullOrWhiteSpace(saveName))throw new InvalidOperationException("Use a simple library filename.");
                library.Save(Hand3DPoseLibrary.Folder+"/"+saveName+".asset");savedSnapshot=EditorJsonUtility.ToJson(library);wasDirty=false;base.SaveChanges();
            }
            catch(Exception error){notice=error.Message;hasUnsavedChanges=true;Repaint();}
        }
        public override void DiscardChanges()
        {
            Restore();if(library!=null && !string.IsNullOrEmpty(savedSnapshot)){EditorJsonUtility.FromJsonOverwrite(savedSnapshot,library);if(!wasDirty)EditorUtility.ClearDirty(library);else EditorUtility.SetDirty(library);fingerprint=JsonUtility.ToJson(library.Data);}base.DiscardChanges();
        }
        private P.Context CurrentContext => context==ContextView.Exploration?P.Context.Exploration:context==ContextView.HandEdit?P.Context.HandEdit:context==ContextView.Action?P.Context.Action:P.Context.GrabDoor|P.Context.GrabDocument;
        private void OnGUI()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorGUILayout.HelpBox("Pose editing and publication are available only in Edit Mode. Play uses the backend and runtime asset selected on Hand3DRig.",MessageType.Info);return;}
            scroll=EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("Preview edits a disposable stage copy. Capture changes only the selected universal profile. Legacy captures are never written.",MessageType.Info);
            var source=(Hand3DRig)EditorGUILayout.ObjectField("TestArmScene rig",rig,typeof(Hand3DRig),true);
            if(source!=rig){Restore();rig=source;}
            var asset=(Hand3DPoseLibrary)EditorGUILayout.ObjectField("Pose library",library,typeof(Hand3DPoseLibrary),false);
            if(asset!=library && asset!=null && (!hasUnsavedChanges || EditorUtility.DisplayDialog("Switch pose library","Discard unsaved edits in the current library?","Discard and switch","Cancel")))Run(()=>{DiscardChanges();Bind(rig,asset);});
            if(GUILayout.Button("Import Phase 3 JSON as a NEW working copy"))Run(()=>
            {
                string path=EditorUtility.OpenFilePanel("Import converted pose profiles",Path.Combine(Directory.GetCurrentDirectory(),"Tools/Hand3DPhase3/Results"),"json");
                if(!string.IsNullOrEmpty(path) && (!hasUnsavedChanges || EditorUtility.DisplayDialog("Import profiles","Discard unsaved library edits?","Discard and import","Cancel"))){DiscardChanges();var imported=Hand3DPoseLibrary.Import(rig,File.ReadAllText(path));Bind(rig,imported);Changed();}
            });
            if(!string.IsNullOrEmpty(notice))EditorGUILayout.HelpBox(notice,MessageType.Error);
            if(library==null || rig==null){EditorGUILayout.EndScrollView();return;}
            EditorGUI.BeginChangeCheck();var nextContext=(ContextView)EditorGUILayout.EnumPopup("Context",context);
            if(EditorGUI.EndChangeCheck()){Restore();context=nextContext;companions=Array.Empty<string>();}
            var available=library.Data.profiles.Where(p=>(p.contexts&CurrentContext)!=0).ToArray();
            if(available.Length>0)
            {
                int index=Array.FindIndex(available,p=>p.id==selectedId);if(index<0){SelectProfile(available[0].id);index=0;}
                int next=EditorGUILayout.Popup("Profile",index,available.Select(Label).ToArray());if(next!=index)SelectProfile(available[next].id);
            }
            else {Restore();selectedId=null;EditorGUILayout.HelpBox("No profiles in this context. Create one below.",MessageType.Info);}
            string[] references=new[]{"(no reference)"}.Concat(library.Data.profiles.Select(p=>p.id)).ToArray();
            int reference=Array.IndexOf(references,referenceId);int newReference=EditorGUILayout.Popup("Reference/base profile",Mathf.Max(0,reference),references);
            if(newReference!=Mathf.Max(0,reference)){Restore();referenceId=newReference==0?null:references[newReference];}
            jointDetails=EditorGUILayout.Foldout(jointDetails,"Companion layers and finger endpoints");
            if(jointDetails)
            {
                foreach(var profile in library.Data.profiles.Where(p=>p.id!=selectedId && p.id!=referenceId && (p.contexts&CurrentContext)!=0))
                {
                    bool active=companions.Contains(profile.id);bool next=EditorGUILayout.ToggleLeft(Label(profile),active);
                    if(next!=active){Restore();companions=next?companions.Concat(new[]{profile.id}).ToArray():companions.Where(id=>id!=profile.id).ToArray();}
                }
                string[] ids=library.Data.profiles.Select(p=>p.id).ToArray();
                int extended=Mathf.Max(0,Array.IndexOf(ids,library.ExtendedId)),flexed=Mathf.Max(0,Array.IndexOf(ids,library.FlexedId));
                int e=EditorGUILayout.Popup("Extended reference",extended,ids),f=EditorGUILayout.Popup("Flexed reference",flexed,ids);
                if(e!=extended || f!=flexed)Run(()=>{Restore();library.SetEndpoints(ids[e],ids[f]);Changed();});
            }
            if(!string.IsNullOrEmpty(selectedId))
            {
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("Preview Pose"))Run(()=>BeginPreview());if(GUILayout.Button("Begin Manual Pose"))Run(()=>BeginPreview(false));if(GUILayout.Button("Restore / Cancel"))Restore();EditorGUILayout.EndHorizontal();
                mask=(Hand3DPosePreview.SelectionMask)EditorGUILayout.EnumPopup("Capture joints",mask);channels=(P.Channels)EditorGUILayout.EnumFlagsField("Capture channels",channels);
                segmentPositions=EditorGUILayout.Toggle("Explicit IK-position permission",segmentPositions);
                EditorGUILayout.HelpBox(segmentPositions?"Segment position capture still requires unchanged local/world lengths and scales. Stretching is rejected.":"Position capture skips IK segments/twists. Root, shoulder and finger positions are allowed.",MessageType.Info);
                showJoints=EditorGUILayout.Foldout(showJoints,"Select preview joints (or use the stage Hierarchy)");
                foreach(var bone in library.Data.skeleton.Where(b=>showJoints || mask==Hand3DPosePreview.SelectionMask.Custom))
                {
                    EditorGUILayout.BeginHorizontal();
                    if(mask==Hand3DPosePreview.SelectionMask.Custom){bool active=custom.Contains(bone.path);bool next=EditorGUILayout.Toggle(active,GUILayout.Width(20));if(next!=active)custom=next?custom.Concat(new[]{bone.path}).ToArray():custom.Where(p=>p!=bone.path).ToArray();}
                    if(GUILayout.Button(bone.role+": "+bone.path,EditorStyles.miniButton))Run(()=>
                    {if(preview==null)throw new InvalidOperationException("Open the preview first.");Selection.activeTransform=preview.Joint(bone.path);SceneView.lastActiveSceneView?.FrameSelected();});
                    EditorGUILayout.EndHorizontal();
                }
                using(new EditorGUI.DisabledScope(preview==null))if(GUILayout.Button("Capture Pose — selected profile only"))Run(Capture);
                DrawProfile();
            }
            EditorGUILayout.Space();EditorGUILayout.LabelField("New visual profile",EditorStyles.boldLabel);newName=EditorGUILayout.TextField("Name",newName);newId=EditorGUILayout.TextField("Stable ID (blank = generated)",newId);
            if(GUILayout.Button("Create empty profile"))Run(()=>
            {Restore();string id=string.IsNullOrWhiteSpace(newId)?"pose."+Guid.NewGuid().ToString("N"):newId;library.AddProfile(id,newName,CurrentContext,false);SelectProfile(id);Changed();});
            using(new EditorGUI.DisabledScope(string.IsNullOrEmpty(selectedId)))if(GUILayout.Button("Duplicate selected profile with new ID"))Run(()=>
            {Restore();string id=string.IsNullOrWhiteSpace(newId)?"pose."+Guid.NewGuid().ToString("N"):newId;library.DuplicateProfile(selectedId,id,newName);SelectProfile(id);Changed();});
            EditorGUILayout.Space();extraBone=(Transform)EditorGUILayout.ObjectField("Register existing joint",extraBone,typeof(Transform),true);extraRole=(S.Role)EditorGUILayout.EnumPopup("Joint role",extraRole);
            if(extraRole==S.Role.Finger)extraFinger=(Finger)EditorGUILayout.EnumPopup("Logical finger",extraFinger);
            if(GUILayout.Button("Add joint to catalog"))Run(()=>{Restore();library.AddJoint(rig,extraBone,extraRole,extraRole==S.Role.Finger?(int)extraFinger:-1);Changed();});
            saveName=EditorGUILayout.TextField("New library filename",saveName);if(GUILayout.Button("Save Library"))SaveChanges();
            DrawPublication();
            EditorGUILayout.LabelField("Storage",Hand3DPoseLibrary.Folder);EditorGUILayout.LabelField("Saved asset",AssetDatabase.GetAssetPath(library));EditorGUILayout.EndScrollView();
        }
        private void DrawPublication()
        {
            EditorGUILayout.Space();EditorGUILayout.LabelField("Publish selected Universal profiles",EditorStyles.boldLabel);
            var next=(Hand3DRuntimePoses)EditorGUILayout.ObjectField("Runtime target",runtimeTarget,typeof(Hand3DRuntimePoses),false);
            if(next!=runtimeTarget){runtimeTarget=next;publicationReview=null;}
            EditorGUILayout.HelpBox("Save Library first. Only checked IDs are added/updated; assignments and other profiles stay unchanged. Test profiles are never selected automatically. New IDs need an explicit runtime/action assignment before Play.",MessageType.Info);
            if(GUILayout.Button("Select current profile only")){publishIds=string.IsNullOrEmpty(selectedId)?Array.Empty<string>():new[]{selectedId};publicationReview=null;}
            foreach(var profile in library.Data.profiles)
            {
                bool chosen=publishIds.Contains(profile.id),checkedNow=EditorGUILayout.ToggleLeft(Label(profile),chosen);
                if(chosen!=checkedNow){publishIds=checkedNow?publishIds.Concat(new[]{profile.id}).ToArray():publishIds.Where(id=>id!=profile.id).ToArray();publicationReview=null;}
            }
            if(GUILayout.Button("Validate and review selected publication"))Run(()=>{Restore();publicationReview=Hand3DRuntimePoseExport.ReviewSelected(rig,library,runtimeTarget,publishIds);});
            if(!string.IsNullOrEmpty(publicationReview))EditorGUILayout.HelpBox(publicationReview,MessageType.Info);
            using(new EditorGUI.DisabledScope(string.IsNullOrEmpty(publicationReview)))
                if(GUILayout.Button("Confirm selective publication..."))Run(()=>
                {
                    Restore();string summary=Hand3DRuntimePoseExport.ReviewSelected(rig,library,runtimeTarget,publishIds);
                    if(!EditorUtility.DisplayDialog("Publish selected Universal profiles",summary,"Publish selected profiles","Cancel"))return;
                    Hand3DRuntimePoseExport.PublishSelected(rig,library,runtimeTarget,publishIds);publicationReview=null;
                });
            initialCopy=EditorGUILayout.Foldout(initialCopy,"Initial migration / new runtime asset (advanced)");
            if(initialCopy)
            {
                EditorGUILayout.HelpBox("Creates only the 13 migrated functional profiles. Publish any additional profiles through the explicit selection above.",MessageType.Info);
                if(GUILayout.Button("Export initial functional runtime COPY"))Run(()=>{Restore();Hand3DRuntimePoseExport.ExportDialog(rig,library);});
            }
        }
        private static string Label(P profile)=>string.IsNullOrEmpty(profile.displayName)?profile.id:profile.displayName+" ["+profile.id+"]";
        private void DrawProfile()
        {
            var next=Hand3DPoseLibrary.Clone(library.Data);var p=next.profiles.Single(p=>p.id==selectedId);
            EditorGUILayout.Space();EditorGUILayout.LabelField("Selected profile data",EditorStyles.boldLabel);EditorGUILayout.SelectableLabel(p.id,GUILayout.Height(18));
            showValues=EditorGUILayout.Foldout(showValues,"Edit joint values, spaces and influence masks");
            EditorGUI.BeginChangeCheck();p.displayName=EditorGUILayout.TextField("Display name",p.displayName);p.contexts=(P.Context)EditorGUILayout.EnumFlagsField("Contexts",p.contexts);p.fingersOnly=EditorGUILayout.Toggle("Fingers-only mask",p.fingersOnly);p.fingerCombination=EditorGUILayout.IntSlider("Finger mask (-1 = unused)",p.fingerCombination,-1,31);
            p.transition.duration=EditorGUILayout.FloatField("Transition duration",p.transition.duration);p.transition.releaseDuration=EditorGUILayout.FloatField("Release duration",p.transition.releaseDuration);p.transition.easing=(P.Easing)EditorGUILayout.EnumPopup("Easing",p.transition.easing);
            if(p.transition.easing==P.Easing.CurveClampedOrSmoothStep)p.transition.curve=EditorGUILayout.CurveField("Curve",p.transition.curve);
            foreach(var entry in p.joints.Where(j=>showValues))
            {
                EditorGUILayout.LabelField(entry.jointPath,EditorStyles.boldLabel);EditorGUILayout.LabelField("Capture / stored channels",entry.captured+" / "+entry.storedChannels);entry.enabled=EditorGUILayout.Toggle("Enabled",entry.enabled);entry.influenceChannels=(P.Channels)EditorGUILayout.EnumFlagsField("Influence channels",entry.influenceChannels);entry.weight=EditorGUILayout.Slider("Weight",entry.weight,0f,1f);
                var space=(P.Space)EditorGUILayout.EnumPopup("Reference space",entry.space);
                if(space!=entry.space){entry.space=space;entry.referenceId=space==P.Space.ParentLocal?next.skeleton.Single(b=>b.path==entry.jointPath).parentId:next.cameraId;}
                entry.operation=(P.Operation)EditorGUILayout.EnumPopup("Operation",entry.operation);
                if((entry.storedChannels&P.Channels.Position)!=0)entry.position=EditorGUILayout.Vector3Field("Position",entry.position);
                if((entry.storedChannels&P.Channels.Rotation)!=0){var angles=EditorGUILayout.Vector3Field("Rotation Euler",entry.sourceEuler);if(angles!=entry.sourceEuler){entry.sourceEuler=angles;entry.rotation=Quaternion.Euler(angles);}}
                if(entry.operation==P.Operation.RestLocalNeutralDelta){entry.neutralCaptured=EditorGUILayout.Toggle("Neutral captured",entry.neutralCaptured);var neutral=EditorGUILayout.Vector3Field("Neutral Euler",entry.neutralEuler);if(neutral!=entry.neutralEuler){entry.neutralEuler=neutral;entry.neutralRotation=Quaternion.Euler(neutral);}}
            }
            if(EditorGUI.EndChangeCheck())Run(()=>{Restore();library.Replace(next,"Edit universal hand pose");Changed();});
        }
    }
}
