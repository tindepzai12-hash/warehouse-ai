using System;
using System.IO;
using UnityEngine;

namespace JarVis.Warehouse.Navigation
{
    [Serializable]
    public sealed class RoompresentMapRegistration
    {
        public int schema=2, mapId=153613;
        public string mapFingerprint, scanFingerprint, checkedUtc;
        public Vector3[] source, observed;
        public bool operatorChecked;
        public static string FilePath => Path.Combine(Application.persistentDataPath,"roompresent-vps-registration-v2.json");
        public bool Validate(string mapHash,string scanHash,Vector3[] expected,out Vector3 translation,out float yaw)
        {
            translation=default;yaw=0;
            if(schema!=2||mapId!=153613||!operatorChecked||string.IsNullOrEmpty(mapHash)||string.IsNullOrEmpty(scanHash)||mapFingerprint!=mapHash||scanFingerprint!=scanHash||expected==null||expected.Length!=3||source==null||source.Length!=3||observed==null||observed.Length!=3)return false;
            for(int i=0;i<3;i++)if(!RoompresentPoseGate.Finite(source[i])||!RoompresentPoseGate.Finite(observed[i])||(source[i]-expected[i]).magnitude>.005f)return false;
            return RoompresentDeliveryAR.TryFit(source,observed,out translation,out yaw,out _);
        }
        public static bool TryRead(RoompresentVpsLocalization vps,Vector3[] expected,out Vector3 translation,out float yaw)
        {
            translation=default;yaw=0;
            try {if(!File.Exists(FilePath))return false;var value=JsonUtility.FromJson<RoompresentMapRegistration>(File.ReadAllText(FilePath));return value!=null&&value.Validate(vps.mapFingerprint,vps.scanFingerprint,expected,out translation,out yaw);}
            catch(Exception){return false;}
        }
        public void Save()
        {
            string temp=FilePath+".tmp";File.WriteAllText(temp,JsonUtility.ToJson(this,true));
            if(File.Exists(FilePath))File.Replace(temp,FilePath,null);else File.Move(temp,FilePath);
        }
    }
}
