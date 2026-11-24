using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace JarVis.Warehouse.Lab
{
    public static class LabFixedArchive
    {
        public const int Limit=4*1024*1024;
        public static void Read(Stream input,out string map,out string manifest)
        {
            if(input.Length>Limit+65536)throw new InvalidDataException("ARCHIVE_TOO_LARGE");
            using(var zip=new ZipArchive(input,ZipArchiveMode.Read,true))
            {
                if(zip.Entries.Count!=2||zip.Entries.Count(e=>e.FullName=="map.json")!=1||zip.Entries.Count(e=>e.FullName=="manifest.json")!=1)throw new InvalidDataException("EXPECTED_TWO_MAP_FILES");
                map=Text(zip.GetEntry("map.json"));manifest=Text(zip.GetEntry("manifest.json"));
            }
        }
        static string Text(ZipArchiveEntry entry)
        {
            if(entry.Length>Limit)throw new InvalidDataException("ENTRY_TOO_LARGE");
            using(var input=entry.Open())using(var output=new MemoryStream())
            {
                byte[] buffer=new byte[8192];int n;while((n=input.Read(buffer,0,buffer.Length))>0){if(output.Length+n>Limit)throw new InvalidDataException("ENTRY_TOO_LARGE");output.Write(buffer,0,n);}
                return new UTF8Encoding(false,true).GetString(output.ToArray());
            }
        }
        public static void Write(string path,string map,string manifest)
        {
            if(Encoding.UTF8.GetByteCount(map)>Limit||Encoding.UTF8.GetByteCount(manifest)>65536)throw new InvalidDataException("MAP_TOO_LARGE");
            using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write))using(var zip=new ZipArchive(output,ZipArchiveMode.Create))
                foreach(var pair in new[]{Tuple.Create("map.json",map),Tuple.Create("manifest.json",manifest)})
                    using(var writer=new StreamWriter(zip.CreateEntry(pair.Item1,CompressionLevel.Fastest).Open(),new UTF8Encoding(false)))writer.Write(pair.Item2);
        }
    }
}
