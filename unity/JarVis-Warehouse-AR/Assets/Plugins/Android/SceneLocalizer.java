package com.teep.atlasar;

import android.app.Activity;
import java.io.*;
import java.nio.*;
import java.util.*;
import org.json.*;
import org.opencv.core.*;
import org.opencv.features2d.*;
import org.opencv.calib3d.Calib3d;
import org.opencv.imgcodecs.Imgcodecs;

/** One dedicated worker owns these Mats. Natural features only: no QR decoder. */
public final class SceneLocalizer {
    private static final int MAX_POINTS=50000, MAX_SIDE=8192, MAX_PIXELS=16*1024*1024;
    // Same 10 cm separation already used to choose a distinct ratio-test neighbour.
    private static final double DISTINCT_DISTANCE_SQUARED=.01;
    private static final double MIN_IMAGE_COVERAGE=.02,MIN_SECOND_AXIS_SPREAD=.025;
    static SIFT sift;
    static DescriptorMatcher matcher;
    static float[] world;
    static Mat train;
    static int count;
    private static boolean ready;

    public static synchronized String initialize(Activity activity) {
        if(ready && matcher!=null && sift!=null && train!=null && world!=null) return "READY:"+count;
        SIFT nextSift=null;
        DescriptorMatcher nextMatcher=null;
        Mat nextTrain=null;
        try {
            reset();
            if(activity==null)throw new IOException("NO_ACTIVITY");
            System.loadLibrary("opencv_java4"); Core.setNumThreads(2);
            byte[] bytes;
            try(InputStream in=activity.getAssets().open("room-features.bin");ByteArrayOutputStream out=new ByteArrayOutputStream()){
                byte[] block=new byte[65536];int n;
                while((n=in.read(block))!=-1){
                    if((long)out.size()+n>12L+MAX_POINTS*131L*4)throw new IOException("DATABASE_SIZE");
                    out.write(block,0,n);
                }
                bytes=out.toByteArray();
            }
            if(bytes.length<12)throw new IOException("DATABASE_HEADER");
            ByteBuffer buffer=ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN);
            if(buffer.getInt()!=0x41544c53||buffer.getInt()!=1)throw new IOException("DATABASE_HEADER");
            int nextCount=buffer.getInt();
            if(nextCount<100||nextCount>MAX_POINTS||bytes.length!=12L+nextCount*131L*4)throw new IOException("DATABASE_SIZE");
            float[] nextWorld=new float[nextCount*3],descriptors=new float[nextCount*128];
            buffer.asFloatBuffer().get(nextWorld);buffer.position(12+nextWorld.length*4);buffer.asFloatBuffer().get(descriptors);
            for(float v:nextWorld)if(!Float.isFinite(v)||Math.abs(v)>10000)throw new IOException("DATABASE_COORDINATE");
            for(float v:descriptors)if(!Float.isFinite(v)||v<0||v>512)throw new IOException("DATABASE_DESCRIPTOR");
            nextTrain=new Mat(nextCount,128,CvType.CV_32F);nextTrain.put(0,0,descriptors);
            nextMatcher=DescriptorMatcher.create(DescriptorMatcher.FLANNBASED);
            nextMatcher.add(Collections.singletonList(nextTrain));nextMatcher.train();
            nextSift=SIFT.create(1100,3,.03,12,1.6);
            // Publish only after every native allocation and training step succeeded.
            train=nextTrain;matcher=nextMatcher;sift=nextSift;world=nextWorld;count=nextCount;ready=true;
            nextTrain=null;nextMatcher=null;nextSift=null;
            return "READY:"+count;
        } catch(Throwable e){return "ERROR:"+e.getClass().getSimpleName()+":"+e.getMessage();}
        finally {
            clear(nextMatcher);clear(nextSift);release(nextTrain);
            if(!ready)reset();
        }
    }

    private static void clear(Algorithm algorithm){if(algorithm!=null)try{algorithm.clear();}catch(Throwable ignored){}}
    private static void release(Mat mat){if(mat!=null)try{mat.release();}catch(Throwable ignored){}}
    private static void reset(){
        ready=false;clear(matcher);clear(sift);release(train);
        matcher=null;sift=null;train=null;world=null;count=0;
    }
    private static double milliseconds(long started){return (System.nanoTime()-started)/1e6;}
    private static JSONObject fail(String reason,int features,int matches,long started) throws JSONException {
        return new JSONObject().put("valid",false).put("reason",reason).put("features",features).put("matches",matches).put("milliseconds",milliseconds(started));
    }
    private static boolean validFrame(byte[] pixels,int width,int height,int minSide){
        return pixels!=null&&width>=minSide&&height>=minSide&&width<=MAX_SIDE&&height<=MAX_SIDE
            &&(long)width*height<=MAX_PIXELS&&pixels.length==(long)width*height;
    }
    private static boolean finite(double[] values){for(double v:values)if(!Double.isFinite(v))return false;return true;}
    private static boolean poseVectors(Mat rv,Mat tv){
        if(rv.total()!=3||tv.total()!=3||rv.type()!=CvType.CV_64F||tv.type()!=CvType.CV_64F)return false;
        double[] r=new double[3],t=new double[3];rv.get(0,0,r);tv.get(0,0,t);
        if(!finite(r)||!finite(t))return false;
        for(double v:r)if(Math.abs(v)>10000)return false;
        for(double v:t)if(Math.abs(v)>1000000)return false;
        return true;
    }
    private static boolean validRotation(double[] m){
        if(!finite(m))return false;
        for(int i=0;i<3;i++)for(int j=i;j<3;j++){
            double dot=0;for(int k=0;k<3;k++)dot+=m[i*3+k]*m[j*3+k];
            if(Math.abs(dot-(i==j?1:0))>1e-3)return false;
        }
        double det=m[0]*(m[4]*m[8]-m[5]*m[7])-m[1]*(m[3]*m[8]-m[5]*m[6])+m[2]*(m[3]*m[7]-m[4]*m[6]);
        return Math.abs(det-1)<1e-3;
    }
    private static double distanceSquared(Point3 a,Point3 b){double x=a.x-b.x,y=a.y-b.y,z=a.z-b.z;return x*x+y*y+z*z;}
    private static boolean distinctPoint(List<Point3> accepted,Point3 candidate){
        for(Point3 point:accepted)if(distanceSquared(point,candidate)<=DISTINCT_DISTANCE_SQUARED)return false;
        return true;
    }
    /** Bounding-box area / image area, and RMS spread along the smaller covariance axis.
     * Coordinates are normalized per image axis, making both metrics resolution independent.
     * A cluster straddling four grid cells or a diagonal strip cannot establish a stable pose.
     */
    private static double[] coverageMetrics(List<Point> points,int width,int height){
        if(points.size()<3||width<=0||height<=0)return new double[]{0,0};
        double minX=1,minY=1,maxX=0,maxY=0,meanX=0,meanY=0;
        for(Point p:points){
            double x=p.x/width,y=p.y/height;
            if(!Double.isFinite(x)||!Double.isFinite(y)||x<0||x>=1||y<0||y>=1)return new double[]{0,0};
            minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);meanX+=x;meanY+=y;
        }
        meanX/=points.size();meanY/=points.size();double xx=0,xy=0,yy=0;
        for(Point p:points){double x=p.x/width-meanX,y=p.y/height-meanY;xx+=x*x;xy+=x*y;yy+=y*y;}
        xx/=points.size();xy/=points.size();yy/=points.size();
        double major=(xx+yy+Math.hypot(xx-yy,2*xy))/2;
        double minor=major>0?Math.max(0,(xx*yy-xy*xy)/major):0;
        return new double[]{(maxX-minX)*(maxY-minY),Math.sqrt(minor)};
    }

    public static synchronized String locate(byte[] pixels,int width,int height,double fx,double fy,double cx,double cy) {
        long started=System.nanoTime();int features=0,matchCount=0;
        Mat image=null,desc=null,K=null,rv=null,tv=null,inliers=null,rot=null,mask=null;
        MatOfDouble distortion=null;
        MatOfKeyPoint keypoints=null;MatOfPoint3f objects=null;MatOfPoint2f images=null,projected=null;
        List<MatOfDMatch> knn=new ArrayList<>();
        try{
            if(!ready||matcher==null||sift==null||train==null||world==null)return fail("DATABASE_NOT_READY",0,0,started).toString();
            if(!validFrame(pixels,width,height,100)||!Double.isFinite(fx)||!Double.isFinite(fy)||!Double.isFinite(cx)||!Double.isFinite(cy)
                ||fx<1e-6||fy<1e-6||fx>1e7||fy>1e7||cx<0||cx>=width||cy<0||cy>=height)return fail("INVALID_CAMERA",0,0,started).toString();
            image=new Mat(height,width,CvType.CV_8U);image.put(0,0,pixels);
            double brightness=Core.mean(image).val[0];
            if(brightness<22)return fail("LOW_LIGHT",0,0,started).put("brightness",brightness).toString();
            keypoints=new MatOfKeyPoint();desc=new Mat();mask=new Mat();sift.detectAndCompute(image,mask,keypoints,desc);
            KeyPoint[] keys=keypoints.toArray();features=keys.length;
            if(desc.rows()<30)return fail("LOW_TEXTURE",features,0,started).toString();
            matcher.knnMatch(desc,knn,8);
            ArrayList<DMatch> candidates=new ArrayList<>();
            for(MatOfDMatch neighbours:knn){
                DMatch[] matches=neighbours.toArray();if(matches.length<2)continue;
                DMatch best=matches[0];float second=Float.POSITIVE_INFINITY;
                if(best.trainIdx<0||best.trainIdx>=count||best.queryIdx<0||best.queryIdx>=keys.length||!Float.isFinite(best.distance)||best.distance<0)continue;
                Point p=keys[best.queryIdx].pt;
                if(!Double.isFinite(p.x)||!Double.isFinite(p.y)||p.x<0||p.x>=width||p.y<0||p.y>=height)continue;
                int a=best.trainIdx*3;
                for(int j=1;j<matches.length;j++){
                    DMatch other=matches[j];if(other.trainIdx<0||other.trainIdx>=count||!Float.isFinite(other.distance)||other.distance<0)continue;
                    int b=other.trainIdx*3;
                    double dx=(double)world[a]-world[b],dy=(double)world[a+1]-world[b+1],dz=(double)world[a+2]-world[b+2];
                    if(dx*dx+dy*dy+dz*dz>DISTINCT_DISTANCE_SQUARED){second=other.distance;break;}
                }
                if(!Float.isFinite(second)||best.distance>=second*.72f||best.distance>260)continue;
                candidates.add(best);
            }
            // Select strongest observations first; repeated views of one map point count once.
            Collections.sort(candidates,new Comparator<DMatch>(){public int compare(DMatch a,DMatch b){return Float.compare(a.distance,b.distance);}});
            ArrayList<Point3> p3=new ArrayList<>();ArrayList<Point> p2=new ArrayList<>();
            HashSet<Integer> usedTrain=new HashSet<>(),usedQuery=new HashSet<>();
            for(DMatch best:candidates){
                if(usedTrain.contains(best.trainIdx)||usedQuery.contains(best.queryIdx))continue;
                int a=best.trainIdx*3;Point3 point=new Point3(world[a],world[a+1],-world[a+2]);
                if(!distinctPoint(p3,point))continue;
                usedTrain.add(best.trainIdx);usedQuery.add(best.queryIdx);p3.add(point);p2.add(keys[best.queryIdx].pt);
            }
            matchCount=p3.size();
            if(matchCount<24)return fail("NOT_ENOUGH_MATCHES",features,matchCount,started).toString();
            return solveCorrespondences(p3,p2,width,height,fx,fy,cx,cy,features,started);
        }catch(Throwable e){
            try{return fail("ENGINE_ERROR:"+e.getClass().getSimpleName(),features,matchCount,started).toString();}
            catch(Exception ignored){return "{\"valid\":false,\"reason\":\"ENGINE_ERROR\",\"milliseconds\":"+milliseconds(started)+"}";}
        } finally{
            for(MatOfDMatch k:knn)release(k);
            for(Mat mat:new Mat[]{image,desc,K,distortion,rv,tv,inliers,rot,mask,keypoints,objects,images,projected})release(mat);
        }
    }


    // Exact same geometric gates for classic and learned matching.
    static String solveCorrespondences(List<Point3> p3,List<Point> p2,int width,int height,
        double fx,double fy,double cx,double cy,int features,long started) {
        int matchCount=p3.size();
        Mat K=null,rv=null,tv=null,inliers=null,rot=null;
        MatOfDouble distortion=null;MatOfPoint3f objects=null;MatOfPoint2f images=null,projected=null;
        try {
            if(matchCount<24)return fail("NOT_ENOUGH_MATCHES",features,matchCount,started).toString();
            K=Mat.eye(3,3,CvType.CV_64F);K.put(0,0,fx);K.put(1,1,fy);K.put(0,2,cx);K.put(1,2,cy);
            distortion=new MatOfDouble();rv=new Mat();tv=new Mat();inliers=new Mat();objects=new MatOfPoint3f();images=new MatOfPoint2f();objects.fromList(p3);images.fromList(p2);
            boolean ok=Calib3d.solvePnPRansac(objects,images,K,distortion,rv,tv,false,1200,4.0f,.995,inliers,Calib3d.SOLVEPNP_EPNP);
            int n=inliers.rows();
            if(!ok||n<22||n<matchCount*.28)return fail("POSE_NOT_CONSISTENT",features,matchCount,started).put("inliers",n).toString();
            if(!poseVectors(rv,tv)||n>matchCount||inliers.type()!=CvType.CV_32S||inliers.cols()!=1)return fail("INVALID_POSE",features,matchCount,started).put("inliers",n).toString();
            ArrayList<Point3> ip3=new ArrayList<>();ArrayList<Point> ip2=new ArrayList<>();int[] ids=new int[n];inliers.get(0,0,ids);
            HashSet<Integer> cells=new HashSet<>(),usedInliers=new HashSet<>();
            for(int index:ids){
                if(index<0||index>=matchCount||!usedInliers.add(index))return fail("INVALID_INLIERS",features,matchCount,started).toString();
                ip3.add(p3.get(index));Point p=p2.get(index);ip2.add(p);cells.add(Math.min(3,(int)(p.x*4/width))+4*Math.min(3,(int)(p.y*4/height)));
            }
            double[] coverage=coverageMetrics(ip2,width,height);
            if(cells.size()<4||coverage[0]<MIN_IMAGE_COVERAGE||coverage[1]<MIN_SECOND_AXIS_SPREAD)
                return fail("MATCHES_TOO_LOCAL",features,matchCount,started).put("inliers",n).put("coverage",coverage[0]).put("secondAxisSpread",coverage[1]).toString();
            objects.fromList(ip3);images.fromList(ip2);Calib3d.solvePnPRefineLM(objects,images,K,distortion,rv,tv);
            if(!poseVectors(rv,tv))return fail("INVALID_POSE",features,matchCount,started).put("inliers",n).toString();
            projected=new MatOfPoint2f();Calib3d.projectPoints(objects,rv,tv,K,distortion,projected);Point[] pp=projected.toArray();double[] errors=new double[n];
            if(pp.length!=n)return fail("INVALID_PROJECTION",features,matchCount,started).toString();
            for(int i=0;i<n;i++){
                errors[i]=Math.hypot(pp[i].x-ip2.get(i).x,pp[i].y-ip2.get(i).y);
                if(!Double.isFinite(errors[i]))return fail("INVALID_PROJECTION",features,matchCount,started).toString();
            }
            Arrays.sort(errors);double median=errors[n/2];
            if(median>2.5||errors[(int)(n*.9)]>5)return fail("HIGH_REPROJECTION_ERROR",features,matchCount,started).put("inliers",n).toString();
            rot=new Mat();Calib3d.Rodrigues(rv,rot);double[] m=new double[9],t=new double[3];rot.get(0,0,m);tv.get(0,0,t);
            if(!validRotation(m))return fail("INVALID_ROTATION",features,matchCount,started).toString();
            double[] C=new double[3],forward=new double[3],up=new double[3];
            for(int j=0;j<3;j++){C[j]=-(m[j]*t[0]+m[3+j]*t[1]+m[6+j]*t[2]);forward[j]=m[6+j];up[j]=-m[3+j];}
            C[2]*=-1;forward[2]*=-1;up[2]*=-1;
            if(!finite(C)||!finite(forward)||!finite(up))return fail("INVALID_POSE",features,matchCount,started).toString();
            // Existing room bounds protect against PnP mirrors without snapping to map.
            if(C[1]<.45||C[1]>2.65||C[0]<-9.5||C[0]>10||C[2]<-6.5||C[2]>5.5)return fail("POSE_OUTSIDE_ROOM",features,matchCount,started).put("inliers",n).toString();
            int positive=0;for(Point3 p:ip3)if(m[6]*p.x+m[7]*p.y+m[8]*p.z+t[2]>.08)positive++;
            if(positive<n*.95)return fail("POSE_BEHIND_CAMERA",features,matchCount,started).toString();
            return new JSONObject().put("valid",true).put("reason","VISUAL_POSE").put("features",features).put("matches",matchCount).put("inliers",n).put("median",median).put("coverage",coverage[0]).put("secondAxisSpread",coverage[1]).put("position",new JSONArray(C)).put("forward",new JSONArray(forward)).put("up",new JSONArray(up)).put("milliseconds",milliseconds(started)).toString();

        }catch(Throwable e){
            try{return fail("GEOMETRY_ERROR:"+e.getClass().getSimpleName(),features,matchCount,started).toString();}
            catch(Exception ignored){return "{\"valid\":false,\"reason\":\"GEOMETRY_ERROR\"}";}
        }finally{for(Mat m:new Mat[]{K,rv,tv,inliers,rot,distortion,objects,images,projected})release(m);}
    }

    /** Optional explicit debug capture. Invoke on the same worker as locate, never on the UI thread.
     * name is a flat .jpg/.jpeg filename. At most 12 images per session directory; no overwrite.
     * Returns OK:<name> or ERROR:<reason>. Encoding does not require a trained matcher.
     */
    public static synchronized String saveFrame(String folder,String name,byte[] gray,int w,int h){
        Mat image=null;File output=null;boolean success=false,created=false;
        try{
            if(!validFrame(gray,w,h,1))return "ERROR:INVALID_FRAME";
            if(folder==null||folder.trim().isEmpty()||name==null||!name.matches("[A-Za-z0-9][A-Za-z0-9._-]{0,119}\\.(?i:jpg|jpeg)"))return "ERROR:INVALID_PATH";
            File root=new File(folder).getCanonicalFile();
            if(!root.isDirectory()&&!root.mkdirs())return "ERROR:CREATE_DIRECTORY";
            output=new File(root,name);
            if(!root.equals(output.getCanonicalFile().getParentFile()))return "ERROR:INVALID_PATH";
            File[] existing=root.listFiles();if(existing==null)return "ERROR:READ_DIRECTORY";
            int images=0;for(File f:existing){String lower=f.getName().toLowerCase(Locale.ROOT);if(lower.endsWith(".jpg")||lower.endsWith(".jpeg"))images++;}
            if(images>=12)return "ERROR:FRAME_LIMIT";
            if(!output.createNewFile())return "ERROR:FILE_EXISTS";created=true;
            System.loadLibrary("opencv_java4");
            image=new Mat(h,w,CvType.CV_8U);image.put(0,0,gray);
            if(!Imgcodecs.imwrite(output.getPath(),image))return "ERROR:ENCODE";
            success=true;return "OK:"+name;
        }catch(Throwable e){return "ERROR:"+e.getClass().getSimpleName();}
        finally{release(image);if(created&&!success&&output!=null)output.delete();}
    }
}
