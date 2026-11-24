using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace AtlasAR
{
    // Small bounded local diagnostic buffer. Images require an explicit UI action.
    public sealed class AtlasDiagnostics
    {
        public string Folder{get;private set;}
        public bool Available{get;private set;}
        public bool Recording{get;private set;}
        public int CapturedImages{get;private set;}
        readonly List<string> lines=new List<string>();float flushedAt,nextImage;int query;
        [Serializable] sealed class BundleManifest {public string appVersion,unityVersion,mapId,createdUtc,localization,navigation;public bool physicalCalibrationVerified,imagesOptIn;}
        public AtlasDiagnostics(AtlasMap map)
        {
            try
            {
                Folder=Path.Combine(Application.persistentDataPath,"Sessions","AtlasAR-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6));Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder,"map.json"),Resources.Load<TextAsset>("Atlas/Map").text);
                var navigation=Resources.Load<TextAsset>("Atlas/NavigationConfig");if(navigation!=null)File.WriteAllText(Path.Combine(Folder,"navigation-config.json"),navigation.text);
                File.WriteAllText(Path.Combine(Folder,"manifest.json"),JsonUtility.ToJson(new BundleManifest{appVersion=Application.version,unityVersion=Application.unityVersion,mapId=map.id,createdUtc=DateTime.UtcNow.ToString("O"),localization="fixed QR A -> session anchor -> ARCore tracking",navigation="Immersal 2.5 sample UI/path + validated Unity NavMesh",physicalCalibrationVerified=false,imagesOptIn=true},true));
                File.WriteAllText(Path.Combine(Folder,"README.txt"),"Atlas AR "+Application.version+"\nLocal diagnostic bundle. positionValid=false means mapPosition is NOT current. Camera image recording is OFF by default. At most 12 grayscale query images are saved after the owner enables recording. No cloud upload. Synthetic tests do not verify room accuracy.\n");Available=true;
            }
            catch(Exception e){Available=false;Debug.LogWarning("Atlas diagnostic storage: "+e.GetType().Name);}
        }
        public void Toggle(){if(!Available)return;Recording=!Recording;nextImage=0;}
        public void Stop(){Recording=false;}
        public string ReserveImage(float now)
        {
            if(!Available||!Recording||query>=12||now<nextImage)return null;
            query++;nextImage=now+3;return "query-"+query.ToString("D4")+".jpg";
        }
        public void CompleteImage(string result){if(result!=null&&result.StartsWith("OK:"))CapturedImages++;}
        public void Append(AtlasLog row){if(!Available)return;lines.Add(JsonUtility.ToJson(row)+"\n");if(lines.Count>=128)Flush();}
        public void Tick(){if(Time.realtimeSinceStartup-flushedAt>=2)Flush();}
        public void Flush()
        {
            flushedAt=Time.realtimeSinceStartup;if(!Available||lines.Count==0)return;
            try{File.AppendAllText(Path.Combine(Folder,"events.jsonl"),string.Concat(lines));lines.Clear();}
            catch(Exception e){Available=false;Recording=false;lines.Clear();Debug.LogWarning("Atlas log write: "+e.GetType().Name);}
        }
    }
}
