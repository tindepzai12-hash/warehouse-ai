using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEngine;
using CompressionLevel=System.IO.Compression.CompressionLevel;

namespace JarVis.Warehouse.Lab
{
    // Numeric diagnostics only. No images, audio, credentials or backend traffic.
    public sealed class LabNavigationJournal
    {
        [Serializable] public sealed class Manifest
        {
            public int schema=1;
            public string id,appVersion,startedUtc,updatedUtc,endedUtc,state="recording",endReason;
            public string coordinateSystem="Unity map-local metres; x/z horizontal; y up; heading degrees";
            public bool physicalAccuracyVerified=false,containsCameraImages=false;
            public int samples,invalidPoseSamples,trackingUnavailableSamples,part=1;
            public double durationSeconds;
        }
        public sealed class Snapshot
        {
            public string id;
            public readonly Dictionary<string,string> text=new Dictionary<string,string>();
            public readonly List<KeyValuePair<string,long>> files=new List<KeyValuePair<string,long>>();
            readonly ExportIntegrity integrity=new ExportIntegrity();
            public void AddFile(string path)
            {
                long source=new FileInfo(path).Length,length=source;
                if(path.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase))length=CompleteLines(path,source);
                files.Add(new KeyValuePair<string,long>(path,length));
                if(source!=length)integrity.trimmed.Add(new TrimmedTail{file=Path.GetFileName(path),sourceBytes=source,capturedBytes=length,droppedTailBytes=source-length});
                text["export-integrity.json"]=JsonUtility.ToJson(integrity,true);
            }
        }
        [Serializable] public sealed class TrimmedTail {public string file;public long sourceBytes,capturedBytes,droppedTailBytes;}
        [Serializable] public sealed class ExportIntegrity
        {public string policy="Only newline-terminated JSONL records exported; source files preserved. See trimmed for incomplete crash tails.";public List<TrimmedTail> trimmed=new List<TrimmedTail>();}
        static long CompleteLines(string path,long length)
        {
            using(var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            {
                var buffer=new byte[4096];long end=length;
                while(end>0)
                {
                    int size=(int)Math.Min(buffer.Length,end);long start=end-size;input.Position=start;int used=0;
                    while(used<size){int read=input.Read(buffer,used,size-used);if(read==0)throw new EndOfStreamException("Log changed during capture");used+=read;}
                    for(int i=size-1;i>=0;i--)if(buffer[i]==10)return start+i+1;
                    end=start;
                }
            }
            return 0;
        }
        public const string CsvHeader="utc,monotonic_seconds,target,tracking,located,pose_valid,raw_x,raw_z,accepted_x,accepted_z,display_x,display_z,heading_deg,remaining_m,route_id,pose_reason\n";
        readonly string root;
        readonly StringBuilder lines=new StringBuilder(),csv=new StringBuilder();
        double startSeconds,nextFlush;
        public Manifest Current {get;private set;}
        public string Folder {get;private set;}
        public string Error {get;private set;}
        public bool Active=>Current!=null&&Current.endedUtc==null;
        public LabNavigationJournal(string root){this.root=root;}
        public void Begin(string version,double now,IDictionary<string,string> context)
        {
            if(Active)End("NEW_JOURNEY",now);
            if(lines.Length>0)throw new IOException("Lượt trước chưa lưu được; giữ nhật ký để thử lại.");
            string id=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff",CultureInfo.InvariantCulture)+"-"+Guid.NewGuid().ToString("N").Substring(0,8);
            string folder=Path.Combine(root,id);Directory.CreateDirectory(folder);
            foreach(var item in context)AtomicText(Path.Combine(folder,item.Key),item.Value);
            Current=new Manifest{id=id,appVersion=version,startedUtc=DateTime.UtcNow.ToString("O")};
            Folder=folder;startSeconds=now;nextFlush=now+1;Error=null;Persist();
        }
        public void Append(string json,string csvRow,double now,bool poseValid,bool tracking)
        {
            if(!Active)return;
            // Unique sequence permits reconciling samples and state transitions across parts.
            lines.Append("{\"sampleIndex\":").Append(Current.samples).Append(",\"data\":").Append(json).Append("}\n");
            csv.Append(csvRow);Current.samples++;
            if(!poseValid)Current.invalidPoseSamples++;if(!tracking)Current.trackingUnavailableSamples++;
            Current.durationSeconds=Math.Max(0,now-startSeconds);Current.updatedUtc=DateTime.UtcNow.ToString("O");
            if(now>=nextFlush||lines.Length>=32768){nextFlush=now+1;Flush();}
            // Retain failed writes in memory, then report rather than consume unbounded RAM.
            if(lines.Length>512*1024)throw new IOException("Không lưu được nhật ký; bộ đệm đầy. Cần trống bộ nhớ điện thoại.");
        }
        public void Pause(bool paused,double now)
        {
            if(!Active)return;Current.state=paused?"paused":"recording";
            Current.durationSeconds=Math.Max(0,now-startSeconds);Flush();
        }
        public void End(string reason,double now)
        {
            if(!Active){Flush();return;}
            Current.endedUtc=DateTime.UtcNow.ToString("O");Current.state="stopped";Current.endReason=reason;
            Current.durationSeconds=Math.Max(0,now-startSeconds);Flush();
        }
        public void Flush()
        {
            if(Current==null)return;
            string log=Path.Combine(Folder,"trace-"+Current.part.ToString("D4")+".jsonl"),table=Path.Combine(Folder,"positions-"+Current.part.ToString("D4")+".csv");
            long logBefore=File.Exists(log)?new FileInfo(log).Length:0,tableBefore=File.Exists(table)?new FileInfo(table).Length:0;
            try
            {
                if(lines.Length>0)
                {
                    AppendFile(log,lines.ToString());AppendFile(table,(tableBefore==0?CsvHeader:"")+csv);
                }
                Persist();lines.Clear();csv.Clear();Error=null;
                if(new FileInfo(log).Exists&&new FileInfo(log).Length>=1024*1024)Current.part++;
            }
            catch(Exception error)
            {
                Rollback(log,logBefore);Rollback(table,tableBefore);Error=error.GetType().Name;throw;
            }
        }
        void Persist()=>AtomicText(Path.Combine(Folder,"manifest.json"),JsonUtility.ToJson(Current,true));
        static void AppendFile(string path,string text)
        {
            byte[] data=Encoding.UTF8.GetBytes(text);
            using(var stream=new FileStream(path,FileMode.Append,FileAccess.Write,FileShare.Read)){stream.Write(data,0,data.Length);stream.Flush(true);}
        }
        static void Rollback(string path,long length){try{if(File.Exists(path))using(var stream=new FileStream(path,FileMode.Open,FileAccess.Write,FileShare.Read))stream.SetLength(length);}catch(Exception){}}
        public static void AtomicText(string path,string text)
        {
            string pending=path+".pending";File.WriteAllText(pending,text,Encoding.UTF8);
            if(File.Exists(path))File.Replace(pending,path,null);else File.Move(pending,path);
        }
        public string LatestFolder()
        {
            if(Folder!=null)return Folder;
            if(!Directory.Exists(root))return null;
            return Directory.GetDirectories(root).OrderByDescending(Path.GetFileName,StringComparer.Ordinal).FirstOrDefault(p=>File.Exists(Path.Combine(p,"manifest.json")));
        }
        public Snapshot Capture(IDictionary<string,string> context)
        {
            Flush();string folder=LatestFolder();if(folder==null)throw new InvalidOperationException("Chưa có lượt chỉ đường đã lưu.");
            var result=new Snapshot{id=Path.GetFileName(folder)};
            foreach(string file in Directory.GetFiles(folder))
            {
                string name=Path.GetFileName(file);
                if(name.EndsWith(".json",StringComparison.Ordinal))result.text[name]=File.ReadAllText(file);
                else if(name.StartsWith("trace-",StringComparison.Ordinal)&&name.EndsWith(".jsonl",StringComparison.Ordinal)||name.StartsWith("positions-",StringComparison.Ordinal)&&name.EndsWith(".csv",StringComparison.Ordinal))result.AddFile(file);
            }
            foreach(var item in context)result.text["export-"+item.Key]=item.Value;
            result.text["README.txt"]="JarVis - lượt chỉ đường\nmanifest.json: lượt ghi, số mẫu, trạng thái và thời gian UTC.\ntrace-*.jsonl: sampleIndex + data (tọa độ, tracking, QR, tuyến, mũi tên, AI).\npositions-*.csv: vị trí x/z theo mét để đối chiếu map 2D; ô trống = chưa có tọa độ hợp lệ.\n*.json: map, mốc và căn chỉnh lúc bắt đầu; export-*.json: trạng thái lúc xuất.\nĐây là nhật ký số, không có ảnh/quay màn hình. Tọa độ và nhãn AI không chứng minh lối đi thực tế đã đúng.\nNếu xuất lúc đang đi, ZIP chỉ lấy dữ liệu tới lúc bấm xuất; app tiếp tục lưu phần sau.\nKhi app bị hệ điều hành buộc dừng, có thể thiếu khoảng một giây cuối chưa ghi xuống đĩa.\n";
            return result;
        }
        // Worker-safe: immutable text and exact lengths; later appends never enter this export.
        public static string WriteZip(Snapshot snapshot,string output)
        {
            Directory.CreateDirectory(output);string final=Path.Combine(output,"JarVis-Navigation-"+snapshot.id+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".zip"),pending=final+".pending";
            try
            {
                using(var zip=ZipFile.Open(pending,ZipArchiveMode.Create))
                {
                    foreach(var item in snapshot.text)using(var writer=new StreamWriter(zip.CreateEntry(item.Key,CompressionLevel.Fastest).Open(),new UTF8Encoding(false)))writer.Write(item.Value);
                    byte[] buffer=new byte[65536];
                    foreach(var item in snapshot.files)
                    using(var input=new FileStream(item.Key,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
                    using(var entry=zip.CreateEntry(Path.GetFileName(item.Key),CompressionLevel.Fastest).Open())
                    {
                        long left=item.Value;while(left>0){int read=input.Read(buffer,0,(int)Math.Min(left,buffer.Length));if(read==0)throw new EndOfStreamException("Nhật ký thay đổi trong lúc xuất.");entry.Write(buffer,0,read);left-=read;}
                    }
                }
                using(var check=ZipFile.OpenRead(pending))if(check.Entries.Count!=snapshot.text.Count+snapshot.files.Count)throw new InvalidDataException("ZIP thiếu dữ liệu.");
                File.Move(pending,final);return final;
            }
            catch{if(File.Exists(pending))File.Delete(pending);throw;}
        }
    }
}
