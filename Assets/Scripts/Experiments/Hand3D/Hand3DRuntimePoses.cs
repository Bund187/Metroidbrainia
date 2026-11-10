using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using P = Metroidbrainia.Hand3DExperiment.Hand3DPoseProfile;

namespace Metroidbrainia.Hand3DExperiment
{
    // Runtime-readable copy, independent of the Editor library and of its experimental/unassigned poses.
    public sealed class Hand3DRuntimePoses : ScriptableObject
    {
        [Serializable] public sealed class Gesture
        {
            public FlexedFingers combination;
            public string[] exploration = Array.Empty<string>();
            public string[] handEdit = Array.Empty<string>();
        }
        [SerializeField] private Hand3DPoseProfileSet data;
        [SerializeField] private string exploration, handEdit, extended, flexed;
        [SerializeField] private Gesture[] gestures = Array.Empty<Gesture>();
        [SerializeField] private string[] grabDoor = Array.Empty<string>(), grabDocument = Array.Empty<string>();
        public Hand3DPoseProfileSet Data => data;
        public string Exploration => exploration;
        public string HandEdit => handEdit;
        public string Extended => extended;
        public string Flexed => flexed;
        public Gesture[] Gestures => gestures;
        public string[] GrabDoor => grabDoor;
        public string[] GrabDocument => grabDocument;
        public P Find(string id) => data?.profiles?.FirstOrDefault(p=>p!=null && p.id==id);
        public Gesture Recognized(FlexedFingers combination) => gestures.FirstOrDefault(g=>g.combination==combination);

        // Explicit authoring/import API; no automatic conversion or mutation on load.
        public void Configure(Hand3DPoseProfileSet source,string explorationId,string editId,string extendedId,string flexedId,Gesture[] bindings,string[] door,string[] document)
        {
            data=JsonUtility.FromJson<Hand3DPoseProfileSet>(JsonUtility.ToJson(source));exploration=explorationId;handEdit=editId;extended=extendedId;flexed=flexedId;gestures=bindings;grabDoor=door;grabDocument=document;
        }
        public string[] ValidateConfiguration()
        {
            var errors=new List<string>();
            if(data==null){errors.Add("Assign runtime pose data.");return errors.ToArray();}
            errors.AddRange(data.Validate());
            foreach(string id in new[]{exploration,handEdit,extended,flexed}.Concat(grabDoor).Concat(grabDocument).Concat(gestures.SelectMany(g=>g.exploration.Concat(g.handEdit))))
                if(Find(id)==null)errors.Add("Missing assigned profile: "+id);
            if(errors.Count>0)return errors.ToArray();
            foreach(string id in new[]{exploration,handEdit})
            {
                var p=Find(id);
                foreach(var joint in data.skeleton.Where(b=>b.role==Hand3DPoseProfileSet.Role.Root || b.role==Hand3DPoseProfileSet.Role.UpperArm || b.role==Hand3DPoseProfileSet.Role.Forearm || b.role==Hand3DPoseProfileSet.Role.Wrist))
                {
                    var required=joint.role==Hand3DPoseProfileSet.Role.Root?P.Channels.Position|P.Channels.Rotation:P.Channels.Rotation;
                    if((p.Influence(joint.path)&required)!=required)errors.Add(id+" lacks required mode channels: "+joint.path);
                }
            }
            foreach(var bone in data.skeleton.Where(b=>b.role==Hand3DPoseProfileSet.Role.Finger))
                foreach(string id in new[]{extended,flexed})if((Find(id).Influence(bone.path)&P.Channels.Rotation)==0)errors.Add(id+" lacks finger endpoint: "+bone.path);
            if(grabDoor.Length==0 || grabDocument.Length==0)errors.Add("Assign both temporary interaction groups.");
            void Contexts(string[] ids,P.Context context){foreach(string id in ids)if((Find(id).contexts&context)==0)errors.Add(id+": incompatible assigned context "+context);}
            Contexts(new[]{exploration},P.Context.Exploration);Contexts(new[]{handEdit},P.Context.HandEdit);Contexts(grabDoor,P.Context.GrabDoor);Contexts(grabDocument,P.Context.GrabDocument);
            foreach(var gesture in gestures){Contexts(gesture.exploration,P.Context.Exploration);Contexts(gesture.handEdit,P.Context.HandEdit);}
            if(gestures.GroupBy(g=>g.combination).Any(g=>g.Count()>1))errors.Add("Duplicate gesture binding.");
            foreach(var gesture in gestures)foreach(string id in gesture.handEdit)
                if(Find(id).joints.Any(j=>j.enabled && j.weight>0 && j.influenceChannels!=0 && data.skeleton.First(b=>b.path==j.jointPath).role!=Hand3DPoseProfileSet.Role.Finger))errors.Add(id+": HandEdit gesture must affect fingers only.");
            foreach(var p in data.profiles)
            {
                if(p.transition==null || float.IsNaN(p.transition.duration) || float.IsInfinity(p.transition.duration) || float.IsNaN(p.transition.releaseDuration) || float.IsInfinity(p.transition.releaseDuration))errors.Add(p.id+": invalid timing.");
                foreach(var j in p.joints)
                {
                    if(!Enum.IsDefined(typeof(P.Space),j.space) || !Enum.IsDefined(typeof(P.Operation),j.operation))errors.Add(p.id+": unsupported reference operation.");
                    if(float.IsNaN(j.position.sqrMagnitude) || float.IsInfinity(j.position.sqrMagnitude))errors.Add(p.id+": non-finite position.");
                    var bone=data.skeleton.FirstOrDefault(b=>b.path==j.jointPath);
                    if(bone==null)continue;
                    if((j.influenceChannels&P.Channels.Position)!=0 && bone.role!=Hand3DPoseProfileSet.Role.Root && bone.role!=Hand3DPoseProfileSet.Role.Shoulder && bone.role!=Hand3DPoseProfileSet.Role.Finger
                        && Vector3.Distance(j.position,bone.sourceLocalPosition)>.000001f)errors.Add(p.id+": runtime IK does not change segment offsets.");
                    if(j.space==P.Space.CameraRelative && j.operation!=P.Operation.Absolute)errors.Add(p.id+": generic camera offsets are not supported.");
                }
            }
            return errors.ToArray();
        }
    }
}
