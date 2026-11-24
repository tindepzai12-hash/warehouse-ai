using System;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    [Serializable] public sealed class LabDepthPolicy
    {
        public float cellSize=.2f,minHeight=.18f,maxHeight=1.8f,minRange=.35f,maxRange=3,ttl=3,edgeRadius=.38f;
        public int confirmFrames=3,minPixels=2;
        public bool Valid=>cellSize>=.1f&&cellSize<=.3f&&minHeight>=.15f&&maxHeight<=2&&maxHeight>minHeight&&
            minRange>=.25f&&maxRange<=4&&maxRange>minRange&&ttl>=1&&ttl<=5&&edgeRadius>=.2f&&edgeRadius<=.5f&&confirmFrames>=3&&minPixels>=2;
    }
    [Serializable] public sealed class LabKnowledgeMarker
    {public string id,positionConfidence;public LabPoint centre,access;public bool orientationVerified;}
    [Serializable] public sealed class LabMapKnowledge
    {
        public int schema;public string id,mapId,floorHash,graphHash,authority;
        public bool physicalAccuracyVerified,globalVisualLocalizationReady;
        public LabKnowledgeMarker[] markers;public LabDepthPolicy depth;
        public bool Matches(LabFloorPlan floor,LabCorridorGraph graph)=>schema==1&&!string.IsNullOrEmpty(id)&&floor!=null&&graph!=null&&
            floor.id==mapId&&floor.geometrySha256==floorHash&&graph.graphHash==graphHash&&graph.floorHash==floorHash&&
            markers!=null&&markers.Length==5&&depth!=null&&depth.Valid;
        public static LabMapKnowledge Load()
        {var data=Resources.Load<TextAsset>("LabTest/LabMapKnowledge");return data==null?null:JsonUtility.FromJson<LabMapKnowledge>(data.text);}
    }
}
