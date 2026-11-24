using System;
using System.Collections.Generic;
using UnityEngine;

namespace JarVis.Warehouse.Lab
{
    // Reuses one numeric grid. Adds a conservative body buffer around observed objects; never opens static obstacles.
    public sealed class LabLocalPathfinder
    {
        readonly LabFloorPlan source,masked;readonly LabFloorRouter router;
        public const float BodyRadius=.30f,ObjectRadius=.18f;
        public LabLocalPathfinder(LabFloorPlan floor)
        {
            source=floor;
            masked=new LabFloorPlan{id=floor.id,geometrySha256=floor.geometrySha256,revision=floor.revision,width=floor.width,height=floor.height,
                resolution=floor.resolution,originX=floor.originX,originZ=floor.originZ,requiredClearance=Math.Max(floor.requiredClearance,BodyRadius),
                cells=new int[floor.cells.Length],clearance=new float[floor.clearance.Length]};
            router=new LabFloorRouter(masked);
        }
        public string Find(LabPoint start,LabPoint target,IReadOnlyList<LabPoint> obstacles,out List<LabPoint> route)
        {
            Array.Copy(source.cells,masked.cells,source.cells.Length);Array.Copy(source.clearance,masked.clearance,source.clearance.Length);
            foreach(var p in obstacles)
            {
                float range=ObjectRadius+BodyRadius+source.resolution;
                int xmin=Math.Max(0,(int)Math.Floor((p.x-range-source.originX)/source.resolution)),xmax=Math.Min(source.width-1,(int)Math.Ceiling((p.x+range-source.originX)/source.resolution));
                int zmin=Math.Max(0,(int)Math.Floor((p.z-range-source.originZ)/source.resolution)),zmax=Math.Min(source.height-1,(int)Math.Ceiling((p.z+range-source.originZ)/source.resolution));
                for(int z=zmin;z<=zmax;z++)for(int x=xmin;x<=xmax;x++)
                {
                    int id=z*source.width+x;if(source.cells[id]!=1)continue;
                    float dx=source.originX+(x+.5f)*source.resolution-p.x,dz=source.originZ+(z+.5f)*source.resolution-p.z;
                    float clearance=(float)Math.Sqrt(dx*dx+dz*dz)-ObjectRadius;
                    masked.clearance[id]=Math.Min(masked.clearance[id],Math.Max(0,clearance));
                    if(clearance<BodyRadius)masked.cells[id]=2;
                }
            }
            return router.Find(start,target,out route);
        }
        public bool Segment(LabPoint a,LabPoint b)=>router.Segment(a,b);
    }
}
