using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace JarVis.Warehouse.Lab
{
    public static class LabPrintedCard
    {
        // Existing 1000x1200 template: black QR outer edges span 506 pixels.
        // This is the IMAGE width, not A4 paper width and not the inner QR width.
        public const float InnerQrMetres=.100f;
        public const float ImageWidthMetres=InnerQrMetres*1000f/506f;
        public const float ImageHeightMetres=ImageWidthMetres*1.2f;
        public const string Template="jarvis-pattern-1000x1200-qr506-v1";
        public static bool Id(string id)=>id!=null&&id.Length==1&&id[0]>='A'&&id[0]<='E';
        public static bool QuaternionValid(float[] q)
        {
            if(q==null||q.Length!=4)return false;
            double n=0;foreach(float v in q){if(float.IsNaN(v)||float.IsInfinity(v))return false;n+=v*v;}
            return Math.Abs(n-1)<.02;
        }
    }
    [Serializable] public sealed class LabFixedMarker
    {
        public string id,utc,template=LabPrintedCard.Template;
        public LabPoint centre,access;
        public LabPoint observedAccess;
        public string accessSource;
        public string captureIssue;
        public float[] rotation;
        public float imageWidth=LabPrintedCard.ImageWidthMetres,qrWidth=LabPrintedCard.InnerQrMetres;
        public int revision=1,samples;
        public bool verified;
    }
    [Serializable] public sealed class LabFixedMap
    {
        public int schema=2,generation;
        public string snapshotId,mapId,floorHash,utc,device;
        public int floorRevision;
        public bool loopChecked,physicalAccepted;
        public string heightSource="Estimated phone height 1.35m; field verification pending";
        public float loopError,loopAngle;
        public LabSurvey survey;
        public List<LabFixedMarker> markers=new List<LabFixedMarker>();
        public string Validate(LabFloorPlan floor,bool published)
        {
            if(schema!=2||generation<0||floor==null||!floor.Valid||mapId!=floor.id||floorHash!=floor.geometrySha256||floorRevision!=floor.revision)return "MAP_REVISION_MISMATCH";
            if(markers==null||markers.Count>5||survey==null||!LabPaths.ValidMap(survey))return "INVALID_DATA";
            var ids=new HashSet<string>();
            foreach(var m in markers)
            {
                if(m==null||!LabPrintedCard.Id(m.id)||!ids.Add(m.id)||!LabPaths.Valid(m.centre)||!LabPaths.Valid(m.access)||!LabPrintedCard.QuaternionValid(m.rotation)||m.revision<1||m.samples<1||m.template!=LabPrintedCard.Template||Math.Abs(m.imageWidth-LabPrintedCard.ImageWidthMetres)>.001f||Math.Abs(m.qrWidth-.1f)>.001f)return "INVALID_MARKER";
                var s=survey.markers.Find(v=>v.id==m.id);
                if(s==null||LabPaths.Distance(s.centre,m.centre)>.001f||LabPaths.Distance(s.access,m.access)>.001f)return "SURVEY_MARKER_MISMATCH";
            }
            if(survey.markers.Count!=markers.Count)return "SURVEY_MARKER_MISMATCH";
            if(!published)return null;
            string integrity=LabSurveyIntegrity.Check(this);
            if(integrity!=null)return integrity;
            if(survey.interrupted)return "SURVEY_INTERRUPTED";
            if(markers.Count<2||!loopChecked||float.IsNaN(loopError)||float.IsNaN(loopAngle)||loopError<0||loopError>.25f||loopAngle<0||loopAngle>5)return "NEED_LOOP_CHECK";
            if(survey.placement==null||!LabRegistration.Solve(survey,floor).valid)return "NEED_MAP_REGISTRATION";
            var router=new LabFloorRouter(floor);
            if(markers.Any(m=>!router.Walkable(m.access)))return "ACCESS_NOT_WALKABLE";
            return null;
        }
    }
    [Serializable] public sealed class LabFixedPointer
    { public int schema=2,generation;public string snapshotId,sha256; }

    // Immutable snapshots; committing the tiny pointer is the only activation step.
    // A crash before pointer replacement leaves the last active snapshot intact.
    public sealed class LabFixedRepository
    {
        readonly string root;
        readonly Func<LabFixedMap,string> encode;
        readonly Func<string,LabFixedMap> decode;
        readonly Func<LabFixedPointer,string> encodePointer;
        readonly Func<string,LabFixedPointer> decodePointer;
        readonly LabFloorPlan floor;
        const int MaximumBytes=4*1024*1024;
        public string DirectoryPath=>root;
        public LabFixedRepository(string directory,LabFloorPlan plan,Func<LabFixedMap,string> serialize,Func<string,LabFixedMap> deserialize,Func<LabFixedPointer,string> serializePointer,Func<string,LabFixedPointer> deserializePointer)
        {root=Path.GetFullPath(directory);floor=plan;encode=serialize;decode=deserialize;encodePointer=serializePointer;decodePointer=deserializePointer;}
        public static string Hash(string text)
        {using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","").ToLowerInvariant();}
        static bool Safe(string id)=>id!=null&&id.Length>=8&&id.Length<=80&&id.All(c=>char.IsLetterOrDigit(c)||c=='-'||c=='_');
        static string Read(string path)
        {var info=new FileInfo(path);if(info.Length>MaximumBytes)throw new InvalidDataException("FILE_TOO_LARGE");return File.ReadAllText(path,Encoding.UTF8);}
        static void Atomic(string path,string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));string tmp=path+".tmp";
            using(var stream=new FileStream(tmp,FileMode.Create,FileAccess.Write,FileShare.None))
            {var bytes=Encoding.UTF8.GetBytes(text);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
            if(File.Exists(path))File.Replace(tmp,path,path+".previous");else File.Move(tmp,path);
        }
        public LabFixedMap LoadActive(out string reason)
        {
            reason="NO_ACTIVE_MAP";
            foreach(string pointer in new[]{Path.Combine(root,"active.json"),Path.Combine(root,"active.json.previous")})
            {
                try
                {
                    if(!File.Exists(pointer))continue;var p=decodePointer(Read(pointer));
                    if(p==null||p.schema!=2||!Safe(p.snapshotId))throw new InvalidDataException("INVALID_POINTER");
                    string text=Read(Path.Combine(root,"snapshots",p.snapshotId,"map.json"));
                    if(Hash(text)!=p.sha256)throw new InvalidDataException("HASH_MISMATCH");
                    var map=decode(text);string error=map?.Validate(floor,true)??(map==null?"INVALID_DATA":null);
                    if(error!=null||map.snapshotId!=p.snapshotId||map.generation!=p.generation)throw new InvalidDataException(error??"POINTER_MISMATCH");
                    reason=pointer.EndsWith(".previous",StringComparison.Ordinal)?"RECOVERED_PREVIOUS":"ACTIVE";return map;
                }
                catch(Exception e) when(e is IOException||e is InvalidDataException||e is ArgumentException||e is UnauthorizedAccessException){reason=e.Message;}
            }
            return null;
        }
        public void SaveDraft(LabFixedMap map)
        {string error=map?.Validate(floor,false)??(map==null?"INVALID_DATA":null);if(error!=null)throw new InvalidDataException(error);Atomic(Path.Combine(root,"draft.json"),encode(map));}
        public LabFixedMap LoadDraft()
        {
            foreach(string name in new[]{"draft.json","draft.json.previous"})
            {try{string path=Path.Combine(root,name);if(!File.Exists(path))continue;var map=decode(Read(path));if(map!=null&&map.Validate(floor,false)==null)return map;}catch(Exception e)when(e is IOException||e is InvalidDataException||e is ArgumentException||e is UnauthorizedAccessException){}}
            return null;
        }
        public void Commit(LabFixedMap map,int expectedGeneration)
        {
            if(map==null||map.Validate(floor,true)!=null)throw new InvalidDataException(map?.Validate(floor,true)??"INVALID_DATA");
            var previous=LoadActive(out _);if((previous?.generation??0)!=expectedGeneration)throw new InvalidOperationException("STALE_GENERATION");
            var candidate=decode(encode(map));candidate.generation=expectedGeneration+1;candidate.snapshotId=DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"-"+Guid.NewGuid().ToString("N");candidate.utc=DateTime.UtcNow.ToString("O");
            string text=encode(candidate),snapshot=Path.Combine(root,"snapshots",candidate.snapshotId);
            Directory.CreateDirectory(snapshot);Atomic(Path.Combine(snapshot,"map.json"),text);
            if(Hash(Read(Path.Combine(snapshot,"map.json")))!=Hash(text))throw new IOException("VERIFY_WRITE_FAILED");
            Atomic(Path.Combine(root,"active.json"),encodePointer(new LabFixedPointer{generation=candidate.generation,snapshotId=candidate.snapshotId,sha256=Hash(text)}));
            map.generation=candidate.generation;map.snapshotId=candidate.snapshotId;map.utc=candidate.utc;
        }
    }
}
