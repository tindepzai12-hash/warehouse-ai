using System;

namespace JarVis.Warehouse.Lab
{
    // A trial bearing resolves yaw while the observed QR centre remains fixed.
    // This is not independent verification that the user aimed at the right physical landmark.
    public static class LabMarkerHeadingAlignment
    {
        public static bool TrySolve(LabPoint origin,LabPoint reference,LabPoint observed,LabPoint camera,
            float forwardX,float forwardZ,out float yaw,out LabPoint translation,out string reason)
        {
            yaw=0;translation=null;reason="INVALID_ALIGNMENT_INPUT";
            if(!LabPaths.Valid(origin)||!LabPaths.Valid(reference)||!LabPaths.Valid(observed)||!LabPaths.Valid(camera)||
                float.IsNaN(forwardX)||float.IsInfinity(forwardX)||float.IsNaN(forwardZ)||float.IsInfinity(forwardZ))return false;
            double mx=reference.x-origin.x,mz=reference.z-origin.z,length=Math.Sqrt(mx*mx+mz*mz);
            double norm=Math.Sqrt(forwardX*forwardX+forwardZ*forwardZ);
            if(length<2||norm<.35){reason="REFERENCE_OR_BEARING_TOO_SHORT";return false;}
            double fx=forwardX/norm,fz=forwardZ/norm,dx=camera.x-observed.x,dz=camera.z-observed.z;
            if(Math.Sqrt(dx*dx+dz*dz)>Math.Min(3,length*.65)){reason="MOVED_TOO_FAR_FROM_SCANNED_CARD";return false;}
            double dot=dx*fx+dz*fz,discriminant=dot*dot-dx*dx-dz*dz+length*length;
            if(discriminant<=0){reason="REFERENCE_RAY_HAS_NO_SOLUTION";return false;}
            double distance=-dot+Math.Sqrt(discriminant);
            if(distance<.5){reason="REFERENCE_IS_NOT_AHEAD";return false;}
            double wx=dx+distance*fx,wz=dz+distance*fz;
            double radians=Math.Atan2(mz*wx-mx*wz,mx*wx+mz*wz),c=Math.Cos(radians),s=Math.Sin(radians);
            yaw=(float)(radians*180/Math.PI);
            translation=new LabPoint((float)(observed.x-c*origin.x-s*origin.z),observed.y-origin.y,
                (float)(observed.z+s*origin.x-c*origin.z));
            reason="QR_CENTRE_PRESERVED_MANUAL_BEARING";return LabPaths.Valid(translation);
        }
    }
}
