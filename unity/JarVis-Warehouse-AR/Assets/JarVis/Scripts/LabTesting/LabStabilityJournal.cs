using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace JarVis.Warehouse.Lab
{
    // Bounded, local only. No camera frames, account data or exception messages.
    public sealed class LabStabilityJournal
    {
        public const int LimitBytes=256*1024;
        readonly object gate=new object();
        readonly string root;
        public string Error {get;private set;}
        public LabStabilityJournal(string directory){root=directory;}
        static string Field(string value,int max)
        {
            if(value==null)return "";value=value.Replace('\r',' ').Replace('\n',' ').Replace('\t',' ');
            return value.Length>max?value.Substring(0,max):value;
        }
        public void Write(string code,string detail)
        {
            lock(gate)
            {
                try
                {
                    Directory.CreateDirectory(root);
                    string file=Path.Combine(root,"runtime.tsv"),previous=Path.Combine(root,"runtime-previous.tsv");
                    byte[] row=Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("O")+"\t"+Field(code,64)+"\t"+Field(detail,1600)+"\n");
                    if(File.Exists(file)&&new FileInfo(file).Length+row.Length>LimitBytes)
                    {File.Copy(file,previous,true);File.Delete(file);}
                    using(var stream=new FileStream(file,FileMode.Append,FileAccess.Write,FileShare.Read))stream.Write(row,0,row.Length);
                    Error=null;
                }
                catch(Exception e){Error=e.GetType().Name;}
            }
        }
        public Dictionary<string,string> Snapshot()
        {
            lock(gate)
            {
                var result=new Dictionary<string,string>();
                foreach(string name in new[]{"runtime.tsv","runtime-previous.tsv"})
                {
                    string file=Path.Combine(root,name);
                    if(File.Exists(file)&&new FileInfo(file).Length<=LimitBytes)result[name]=File.ReadAllText(file);
                }
                return result;
            }
        }
    }
}
