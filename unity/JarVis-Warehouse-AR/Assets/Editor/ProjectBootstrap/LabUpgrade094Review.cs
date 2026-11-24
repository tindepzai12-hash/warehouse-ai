using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using JarVis.Warehouse.Lab;
using UnityEngine;

namespace ProjectBootstrap
{
    public static class LabUpgrade094Review
    {
        const string Output="Logs/LabTest/Upgrade094";static int checks;
        static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);checks++;}
        static LabFloorPlan Floor(int width=80,int height=40)
        {
            return new LabFloorPlan{id="TEST",geometrySha256="TEST",revision=1,width=width,height=height,resolution=.1f,originX=0,originZ=0,requiredClearance=.25f,cells=Enumerable.Repeat(1,width*height).ToArray(),clearance=Enumerable.Repeat(2f,width*height).ToArray()};
        }
        public static void RunAndBuild(){Run();LabTestBuild.Build();}
        public static void Run()
        {
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/result.txt","RUNNING");checks=0;
            LabUpgrade093Review.Run();
            var floor=Floor();LabPoint a=new LabPoint(1,0,2),b=new LabPoint(7,0,2),obstacle=new LabPoint(4,0,2);
            var local=new LabLocalPathfinder(floor);var before=floor.cells.ToArray();
            Check(local.Find(a,b,new[]{obstacle},out var route)==null,"Wide aisle must permit a local bypass");
            Check(route.Count>=3,"Bypass cannot be a straight path through the object");
            for(int i=1;i<route.Count;i++)
            {Check(local.Segment(route[i-1],route[i]),"Every bypass segment must retain body clearance");Check(LabObstacleMemory.DistanceToSegment(obstacle,route[i-1],route[i])>.47f,"Body footprint must clear obstacle radius");}
            Check(before.SequenceEqual(floor.cells),"Dynamic layer never edits static floor");
            Check(local.Find(a,b,Array.Empty<LabPoint>(),out var cleared)==null&&cleared.Count==2,"Expired obstacle mask must reset rather than accumulate");
            for(int z=0;z<floor.height;z++)for(int x=36;x<45;x++)floor.cells[z*floor.width+x]=2;
            Check(local.Find(a,b,Array.Empty<LabPoint>(),out _)!=null,"Static wall cannot be opened");
            var narrow=Floor(80,8);var narrowPlanner=new LabLocalPathfinder(narrow);
            Check(narrowPlanner.Find(new LabPoint(1,0,.4f),new LabPoint(7,0,.4f),new[]{new LabPoint(4,0,.4f)},out _)!=null,"No bypass through a fully blocked narrow aisle");
            var evidence=new LabFloorEvidence(new LabFloorRouter(Floor()));var line=new List<LabPoint>{a,b};
            Check(evidence.FrontCoverage(line,1)==0,"No image does not mean free floor");
            var observations=new List<LabPoint>();for(float x=1.5f;x<3.2f;x+=.1f)for(float z=1.6f;z<2.4f;z+=.1f)observations.Add(new LabPoint(x,0,z));
            evidence.Observe(1,1,1,observations);Check(evidence.FrontCoverage(line,1)==0,"One observation is not confirmation");
            evidence.Observe(1,1,1.2,observations);Check(evidence.FrontCoverage(line,1.2)==0,"Repeated timestamp does not add votes");
            evidence.Observe(1,1.2,1.2,observations);Check(evidence.FrontCoverage(line,1.2)>=.7f,"Fresh repeated floor supports the front route segment");
            Check(evidence.FrontCoverage(line,5)==0,"Floor confirmation expires");
            evidence.Reset(2);Check(evidence.FrontCoverage(line,1.3)==0,"Pose epoch reset invalidates prior floor evidence");
            string root=Path.GetFullPath(Output+"/suite-"+Guid.NewGuid().ToString("N"));
            var suite=new LabTestSuite(root,"0.9.4","SYNTHETIC_EDITOR");
            Check(LabTestSuite.Missions.Length==15,"All guided missions available");
            Check(LabTestSuite.Missions[0].start=="B"&&LabTestSuite.Missions[0].target=="C","Protocol tests B to C without A");
            Check(suite.Begin(1,new Dictionary<string,string>{{"map.json","{\"id\":\"TEST\"}"}}),"Can begin first case");
            suite.Append("heartbeat","{\"tracking\":true}",1.2);suite.Append("depth","{\"depth\":1.4}",1.4);
            var first=suite.Active.id;suite.Finish("USER_ISSUE",2);suite.Begin(3,new Dictionary<string,string>());
            Check(suite.Active.id!=first&&suite.Manifest.attempts.Count==2,"Repeats keep separate attempts");
            suite.Append("heartbeat","{\"tracking\":false}",3.1);suite.Flush();
            suite.Active.traceFrames=7;var captured=suite.Capture();suite.Active.traceFrames=99;suite.Flush();
            string zip=LabNavigationJournal.WriteZip(captured,Output+"/exports");
            using(var file=ZipFile.OpenRead(zip))
            {
                Check(file.Entries.Any(e=>e.FullName=="suite-manifest.json"),"Bundle includes manifest");
                Check(file.Entries.Any(e=>e.FullName=="missions.json"),"Bundle includes instructions");
                using(var reader=new StreamReader(file.GetEntry("suite-manifest.json").Open()))
                    Check(JsonUtility.FromJson<LabSuiteManifest>(reader.ReadToEnd()).attempts.Last().traceFrames==7,"Background ZIP uses frozen manifest even while live test continues");
                Check(file.Entries.Any(e=>e.FullName==first+"-depth.jsonl"),"Bundle includes metric depth sample stream");
                Check(file.Entries.Any(e=>e.FullName==suite.Active.id+"-heartbeat.jsonl"),"Active partial case exported without stopping it");
                foreach(var entry in file.Entries){using(var reader=new StreamReader(entry.Open()))Check(reader.ReadToEnd().Length>0,"Each ZIP entry readable");}
            }
            suite.Dispose();suite=new LabTestSuite(root,"0.9.4","SYNTHETIC_EDITOR");
            Check(suite.Active==null&&suite.Manifest.attempts.Last().result=="INTERRUPTED","Restart preserves partial case as interrupted");
            suite.Select(1);Check(suite.Selected.id=="02","Next case stored");suite.SkipSelected(4);
            Check(suite.Manifest.attempts.Last().result=="SKIPPED"&&suite.Selected.id=="03","Skipped cases explicitly recorded");suite.Dispose();
            LabTestSuiteUiReview.Run();
            File.WriteAllText(Output+"/result.txt","PASS "+checks+" checks + guided-test UI at two sizes + 0.9.3 regression suite. Synthetic only; physical lab acceptance NOT verified.");
            Debug.Log("JARVIS_094_PASS "+checks);
        }
    }
}
