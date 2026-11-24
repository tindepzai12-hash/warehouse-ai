using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    [Serializable] public sealed class LabSuiteMission
    {public string id,title,start,target,instructions;public int seconds;}
    [Serializable] public sealed class LabSuiteAttempt
    {
        public string id,mission,startUtc,endUtc,result="RUNNING",status="RUNNING";
        public int heartbeats,validPoses,trackingLost,depthFrames,traceFrames;public long bytes;
        public double started,ended;public string expectedStart,target;
    }
    [Serializable] public sealed class LabSuiteManifest
    {
        public string schema="JARVIS_TEST_SUITE_1",id,version,device,startedUtc;
        public bool physicalAcceptanceVerified=false,automaticLearning=false;
        public int selected;public List<LabSuiteAttempt> attempts=new List<LabSuiteAttempt>();
    }
    [Serializable] public struct LabDepthSample {public float u,v,depth,x,y,z;}
    // A repeatable offline collection protocol. User verdicts are never converted into automatic acceptance.
    public sealed class LabTestSuite : IDisposable
    {
        public const long Limit=50*1024*1024;
        public static readonly LabSuiteMission[] Missions={
            M("01","Căn tại B, giữ yên","B","C",20,"Đứng ở B, chọn bài rồi quét B (không quét A). Làm bước căn hướng nếu app yêu cầu. Giữ yên 20 giây. Kiểm tra vị trí ở B, đích C và mũi tên không nhảy. Chỉ xác nhận khi đã tự kiểm tra."),
            M("02","Đi từ B tới C","B","C",60,"Bắt đầu ở B. Đi chậm tới C theo lối thực tế; không băng qua bàn. Kiểm tra hướng rẽ, khoảng cách còn lại, vị trí trên map và biểu tượng C. Nếu chưa ở B, đến B trước khi bắt đầu."),
            M("03","Xoay tại C, không bước","C","B",30,"Đứng tại C. Xoay người chậm 90°, 180° rồi 360°, giữ điện thoại trước mặt. Kiểm tra map không dịch ra ngoài phòng; khi đích ở sau lưng, app phải yêu cầu quay đúng hướng. Không vừa đi vừa xoay."),
            M("04","Quay về B","C","B",60,"Đi từ C về B. Quan sát hướng ngược lại, khoảng cách giảm và mũi tên chuyển từ rẽ sang đi thẳng đúng lúc. Đánh dấu Có lỗi nếu app bắt quay về A để tiếp tục."),
            M("05","Đi đoạn dài B tới A","B","A",120,"Bắt đầu ở B, đi tới A qua một lối thông thực tế. Theo dõi chấm vị trí và mũi tên. Đứng lại nếu chỉ xuyên bàn hoặc ra ngoài map; đừng đi theo mũi tên sai để hoàn thành bài."),
            M("06","Quay đầu tại A","A","B",25,"Đứng tại A, xoay khỏi lối 180° rồi nhìn lại. Mũi tên/hướng trên đầu phải yêu cầu quay về phía tuyến, không giữ ĐI THẲNG khi đang nhìn ngược."),
            M("07","Nhiều lối A tới C","A","C",120,"Từ A chọn một lối đi hợp lệ khác tuyến mặc định nếu phòng có lối đó. Kiểm tra tính lại tuyến, rẽ trái/phải và tới C. Không vượt vùng bàn hoặc lối bị chặn. Mở map để đối chiếu tuyến."),
            M("08","Vật cản và lối vòng rộng","C","B",60,"Trước bài, đặt một hộp/ghế đứng yên giữa lối đủ rộng, chừa lối vòng thực tế. Đứng cách vật cản khoảng 1–3 m, nhìn chậm xuống sàn hai bên rồi mới đi. Chờ app xác nhận sàn phía trước. Quan sát tuyến có vòng quanh đồ vật thay vì xuyên qua. Không đứng thử sát vật cản."),
            M("09","Lối bị chắn, không đủ rộng","B","C",45,"Chỉ thử khi có thể đứng yên quan sát một lối hẹp đang bị chắn. App phải giữ/dừng hoặc chọn lối khác đã biết, không vẽ xuyên đồ vật. Không tự bước vào chỗ chật. Bỏ qua nếu không bố trí được an toàn."),
            M("10","Che camera ngắn và khôi phục","B","C",30,"Đứng yên ở lối trống. Che camera khoảng 1 giây rồi bỏ tay, nhìn lại khu vực vừa thấy. Kiểm tra mũi tên dừng khi mất tracking và giữ đích C khi khôi phục; không quay về A. Không di chuyển trong lúc che camera."),
            M("11","Chuyển app rồi quay lại","B","C",30,"Đứng yên. Chuyển sang màn hình chính 3–5 giây rồi mở lại JarVis. Chờ tracking trở lại, kiểm tra đích và dữ liệu bài còn giữ. Không đi khi đang chuyển app."),
            M("12","Đổi đích tại vị trí hiện tại","C","A",45,"Bắt đầu ở C. Mở map/đổi đích sang D hoặc E đã lưu, sau đó về A nếu muốn. Không quét A để đổi đích. Kiểm tra app tính từ chỗ đang đứng và giữ thông tin hướng đi. Nhật ký lưu cả đích thực bạn chọn."),
            M("13","Đo độ lệch trên đoạn 1 m","B","C",30,"Nếu có thước: đánh dấu hai điểm trên sàn cách 1 m, đứng điểm đầu rồi đi thẳng tới điểm cuối, dừng 5 giây. Tự kiểm tra độ dịch của chấm map. Nếu chưa có khoảng cách đo thật, bỏ qua; không dùng bước chân làm số đo chuẩn."),
            M("14","Ánh sáng và mặt sàn","C","B",40,"Đứng yên ở vùng đủ sáng, lia chậm từ đồ vật xuống sàn và sang trái/phải. So sánh báo độ sâu/sàn. Nếu khu vực tối tự nhiên, quan sát thông báo; không tắt đèn khi có người đang đi. Nhãn bàn/ghế không thay cho số đo chiều sâu."),
            M("15","Lần lượt tới D và E","B","D",120,"Từ B đi tới D; sau đó dùng map đổi đích tới E đã lưu. Kiểm tra cả hai điểm và nhiều hướng tiếp cận. Nếu mốc D/E chưa khớp thực tế, đánh dấu Có lỗi và dừng; đừng ghi đè mốc để che sai số.")
        };
        static LabSuiteMission M(string id,string title,string start,string target,int seconds,string instructions)=>new LabSuiteMission{id=id,title=title,start=start,target=target,seconds=seconds,instructions=instructions};
        public readonly string Folder;public LabSuiteManifest Manifest {get;private set;}
        public LabSuiteAttempt Active=>Manifest.attempts.LastOrDefault(a=>a.result=="RUNNING");
        public LabSuiteMission Selected=>Missions[Math.Max(0,Math.Min(Missions.Length-1,Manifest.selected))];
        public string Error {get;private set;}
        readonly Dictionary<string,StreamWriter> writers=new Dictionary<string,StreamWriter>();
        double nextFlush;long total;
        public LabTestSuite(string root,string version,string device)
        {
            Directory.CreateDirectory(root);
            var latest=Directory.GetDirectories(root).OrderByDescending(x=>x,StringComparer.Ordinal).FirstOrDefault();
            if(latest!=null&&File.Exists(Path.Combine(latest,"suite-manifest.json")))
            {
                try{Manifest=JsonUtility.FromJson<LabSuiteManifest>(File.ReadAllText(Path.Combine(latest,"suite-manifest.json")));
                    if(Manifest?.schema!="JARVIS_TEST_SUITE_1"||Manifest.attempts==null||Manifest.version!=version)Manifest=null;}
                catch(Exception){Manifest=null;}
            }
            if(Manifest!=null)
            {
                Folder=latest;
                foreach(var a in Manifest.attempts.Where(a=>a.result=="RUNNING")){a.result="INTERRUPTED";a.status="INTERRUPTED_AFTER_RESTART";a.endUtc=DateTime.UtcNow.ToString("O");}
            }
            else
            {
                string id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
                Folder=Path.Combine(root,id);Directory.CreateDirectory(Folder);
                Manifest=new LabSuiteManifest{id=id,version=version,device=device,startedUtc=DateTime.UtcNow.ToString("O")};
            }
            total=Directory.GetFiles(Folder).Sum(f=>new FileInfo(f).Length);Save();
        }
        public void SkipSelected(double now)
        {
            if(Active!=null)return;
            if(!Manifest.attempts.Any(a=>a.mission==Selected.id))Manifest.attempts.Add(new LabSuiteAttempt{id=(Manifest.attempts.Count+1).ToString("D3")+"-"+Selected.id,mission=Selected.id,expectedStart=Selected.start,target=Selected.target,result="SKIPPED",status="USER_SKIPPED",startUtc=DateTime.UtcNow.ToString("O"),endUtc=DateTime.UtcNow.ToString("O"),started=now,ended=now});
            Select(1);
        }
        public void Select(int delta)
        {
            if(Active!=null)throw new InvalidOperationException("Finish current case first");
            Manifest.selected=Math.Max(0,Math.Min(Missions.Length-1,Manifest.selected+delta));Save();
        }
        public bool Begin(double now,Dictionary<string,string> context)
        {
            if(Active!=null||total>=Limit)return false;
            var m=Selected;string attempt=(Manifest.attempts.Count+1).ToString("D3")+"-"+m.id;
            foreach(var pair in context)
            {
                string shared=Path.Combine(Folder,"context-"+Path.GetFileName(pair.Key));
                if(File.Exists(shared)&&File.ReadAllText(shared)==pair.Value)continue;
                string file=File.Exists(shared)?Path.Combine(Folder,attempt+"-context-"+Path.GetFileName(pair.Key)):shared;
                int bytes=Encoding.UTF8.GetByteCount(pair.Value);if(total+bytes>Limit)return false;
                File.WriteAllText(file,pair.Value,new UTF8Encoding(false));total+=bytes;
            }
            Manifest.attempts.Add(new LabSuiteAttempt{id=attempt,mission=m.id,expectedStart=m.start,target=m.target,startUtc=DateTime.UtcNow.ToString("O"),started=now});
            Error=null;Save();return true;
        }
        public void Append(string kind,string json,double now)
        {
            if(Active==null||Error!=null)return;
            try
            {
                int size=Encoding.UTF8.GetByteCount(json)+1;
                if(total+size>Limit){Error="STORAGE_LIMIT_50_MB";Active.status=Error;Save();return;}
                string name=Active.id+"-"+kind+".jsonl";
                if(!writers.TryGetValue(name,out var writer))
                {writer=new StreamWriter(new FileStream(Path.Combine(Folder,name),FileMode.Append,FileAccess.Write,FileShare.ReadWrite),new UTF8Encoding(false));writers.Add(name,writer);}
                writer.WriteLine(json);total+=size;Active.bytes+=size;
                if(now>=nextFlush){nextFlush=now+2;Flush();}
            }
            catch(Exception e){Error=e.GetType().Name;Active.status="WRITE_FAILED_"+Error;}
        }
        public void Finish(string verdict,double now)
        {
            if(Active==null)return;
            if(verdict!="USER_OK"&&verdict!="USER_ISSUE"&&verdict!="STOPPED")throw new ArgumentException("Unknown verdict");
            var a=Active;Flush();a.result=verdict;a.endUtc=DateTime.UtcNow.ToString("O");a.ended=now;
            a.status=Error==null?"SAVED_USER_VERDICT":"PARTIAL_"+Error;CloseWriters();Save();
        }
        public void Flush(){foreach(var w in writers.Values)w.Flush();Save();}
        void Save()
        {
            string path=Path.Combine(Folder,"suite-manifest.json"),pending=path+".pending",backup=path+".previous";
            File.WriteAllText(pending,JsonUtility.ToJson(Manifest,true),new UTF8Encoding(false));
            if(File.Exists(path))File.Replace(pending,path,backup);else File.Move(pending,path);
        }
        public LabNavigationJournal.Snapshot Capture()
        {
            Flush();var snapshot=new LabNavigationJournal.Snapshot{id="TestSuite-"+Manifest.id};
            snapshot.text["suite-manifest.json"]=JsonUtility.ToJson(Manifest,true);
            foreach(var file in Directory.GetFiles(Folder).Where(f=>!f.EndsWith(".pending")&&!f.EndsWith(".previous")&&Path.GetFileName(f)!="suite-manifest.json"))snapshot.AddFile(file);
            snapshot.text["missions.json"]=JsonUtility.ToJson(new Catalog{missions=Missions},true);
            snapshot.text["README.txt"]="Offline guided tests. USER_OK is a human report, not acceptance. Expected origin is an instruction, not a forced pose. No RGB video, accounts, or audio captured. Depth is optical camera Z in metres; points are approximate map coordinates, not ground truth. No on-device learning. Repeated cases have distinct IDs. Partial cases retained. See suite-manifest.json, heartbeat, navigation and depth JSONL.";
            return snapshot;
        }
        [Serializable] sealed class Catalog {public LabSuiteMission[] missions;}
        void CloseWriters(){foreach(var w in writers.Values)w.Dispose();writers.Clear();}
        public void Dispose(){try{Flush();}finally{CloseWriters();}}
    }
}
