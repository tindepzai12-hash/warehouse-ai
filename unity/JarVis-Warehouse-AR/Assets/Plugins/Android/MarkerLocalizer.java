package com.teep.atlasar;

import android.app.Activity;
import java.io.*;
import java.nio.*;
import java.security.MessageDigest;
import java.util.*;
import org.json.*;
import org.opencv.core.*;
import org.opencv.features2d.*;
import org.opencv.calib3d.Calib3d;
import org.opencv.objdetect.QRCodeDetector;

/** Offline initialization from the OLD fixed printed A tabletop card.
 * Own worker: initialize/locate are serialized. Raw packed luma and K MUST share
 * the same exposure, dimensions and optical axes. Never rotate for display here.
 * Retains one bounded database/SIFT/matcher/decoder; every frame Mat is released.
 * Unity owns exposure synchronization, gravity/yaw gating and pose consensus.
 * This class neither changes Map.json nor changes SceneLocalizer's gates.
 */
public final class MarkerLocalizer {
    private MarkerLocalizer() {}
    private static final int MAGIC=0x4D4B4131, MAX_DATABASE_BYTES=9*1024*1024;
    private static final int MAX_SIDE=1920, MAX_PIXELS=1920*1080;
    private static final int MIN_MATCHES=24, MIN_INLIERS=22, MIN_QR_INLIERS=10;
    private static final double RATIO=.72, MAX_BIT_ERROR=.12, MIN_BIT_MARGIN=.08;
    private static final String PAYLOAD="JWM:MAIN:A";
    private static final ArrayList<Template> templates=new ArrayList<>();
    private static SIFT sift;
    private static DescriptorMatcher matcher;
    // QRCodeDetector has no public release/clear in OpenCV 4.12 Java. Allocate
    // once after library load and retain, including across a failed DB load.
    private static QRCodeDetector decoder;
    private static double[] centre, basis;
    private static boolean ready;

    private static final class Template {
        String id; int width,height,n,grid;
        float[] xy; double[] bounds; byte[] bits; Mat desc;
    }
    private static final class Evidence {
        Template template; int matches,inliers,qrInliers;
        ArrayList<Point> source=new ArrayList<>(),image=new ArrayList<>();
        double[] h; double bitError,bitMargin; String bitWinner;
    }
    private static final class Pose {
        double[] camera,forward,up; double rms,median,p90; int positive;
    }
    private static void release(Mat m){if(m!=null)m.release();}
    private static void clear(Algorithm a){if(a!=null)a.clear();}
    private static void reset(){
        ready=false;
        for(Template t:templates)release(t.desc);
        templates.clear();clear(matcher);clear(sift);matcher=null;sift=null;
        centre=null;basis=null;
    }
    private static byte[] read(Activity activity,String name,int limit)throws IOException{
        try(InputStream in=activity.getAssets().open(name);ByteArrayOutputStream out=new ByteArrayOutputStream()){
            byte[] block=new byte[16384];int n;
            while((n=in.read(block))!=-1){
                if((long)out.size()+n>limit)throw new IOException("ASSET_SIZE");
                out.write(block,0,n);
            }
            return out.toByteArray();
        }
    }
    private static String sha256(byte[] bytes)throws Exception{
        StringBuilder s=new StringBuilder();
        for(byte b:MessageDigest.getInstance("SHA-256").digest(bytes))s.append(String.format(Locale.ROOT,"%02x",b&255));
        return s.toString();
    }
    private static double[] numbers(JSONArray a,int n)throws Exception{
        if(a.length()!=n)throw new IOException("CONFIG_SIZE");
        double[] v=new double[n];for(int i=0;i<n;i++)v[i]=a.getDouble(i);
        if(!finite(v))throw new IOException("CONFIG_NONFINITE");return v;
    }
    public static synchronized String initialize(Activity activity){
        if(ready)return "READY:A:"+templates.get(0).n;
        try{
            reset();if(activity==null)throw new IOException("NO_ACTIVITY");
            System.loadLibrary("opencv_java4");Core.setNumThreads(2);
            JSONObject config=new JSONObject(new String(read(activity,"marker-a-config.json",65536),"UTF-8"));
            if(config.getInt("schema")!=1||!"A".equals(config.getString("startupMarker"))||!PAYLOAD.equals(config.getString("payload")))throw new IOException("CONFIG_IDENTITY");
            if(Math.abs(config.getDouble("widthMetres")-.2)>1e-12||Math.abs(config.getDouble("heightMetres")-.24)>1e-12)throw new IOException("CONFIG_SCALE");
            centre=numbers(config.getJSONArray("centreUnity"),3);
            basis=numbers(config.getJSONArray("basisRhRowMajor"),9);
            // Tabletop is exactly horizontal; basis is a proper RH rotation.
            if(!rotation(basis)||Math.abs(basis[3])>1e-9||Math.abs(basis[4])>1e-9||Math.abs(Math.abs(basis[5])-1)>1e-9)throw new IOException("CONFIG_BASIS");
            byte[] bytes=read(activity,"marker-a-features.bin",MAX_DATABASE_BYTES);
            if(!sha256(bytes).equals(config.getString("databaseSha256")))throw new IOException("DATABASE_HASH");
            ByteBuffer b=ByteBuffer.wrap(bytes).order(ByteOrder.LITTLE_ENDIAN);
            if(b.getInt()!=MAGIC||b.getInt()!=1||b.getInt()!=5)throw new IOException("DATABASE_HEADER");
            for(int index=0;index<5;index++){
                Template t=new Template();t.id=Character.toString((char)b.getInt());
                t.width=b.getInt();t.height=b.getInt();t.n=b.getInt();t.grid=b.getInt();
                if(!t.id.equals(Character.toString((char)('A'+index)))||t.width!=1000||t.height!=1200||t.n<100||t.n>2400||t.grid!=21)throw new IOException("DATABASE_TEMPLATE");
                t.bounds=new double[4];for(int j=0;j<4;j++)t.bounds[j]=b.getFloat();
                if(!finite(t.bounds)||t.bounds[0]<0||t.bounds[1]<0||t.bounds[2]>=t.width||t.bounds[3]>=t.height||t.bounds[2]-t.bounds[0]<400||t.bounds[3]-t.bounds[1]<400)throw new IOException("DATABASE_QR");
                t.xy=new float[t.n*2];float[] descriptors=new float[t.n*128];
                for(int j=0;j<t.xy.length;j++){
                    float v=b.getFloat();if(!Float.isFinite(v)||v<0||v>=(j%2==0?t.width:t.height))throw new IOException("DATABASE_POINT");t.xy[j]=v;
                }
                for(int j=0;j<descriptors.length;j++){
                    float v=b.getFloat();if(!Float.isFinite(v)||v<0||v>512)throw new IOException("DATABASE_DESCRIPTOR");descriptors[j]=v;
                }
                t.bits=new byte[t.grid*t.grid];b.get(t.bits);
                for(byte bit:t.bits)if(bit!=0&&bit!=1)throw new IOException("DATABASE_BITS");
                templates.add(t);t.desc=new Mat(t.n,128,CvType.CV_32F);t.desc.put(0,0,descriptors);
            }
            if(b.hasRemaining())throw new IOException("DATABASE_TRAILING_BYTES");
            sift=SIFT.create(1600,3,.03,12,1.6);
            matcher=DescriptorMatcher.create(DescriptorMatcher.BRUTEFORCE);
            if(decoder==null)decoder=new QRCodeDetector();
            ready=true;return "READY:A:"+templates.get(0).n;
        }catch(Throwable e){
            reset();return "ERROR:"+e.getClass().getSimpleName()+":"+e.getMessage();
        }
    }
    private static boolean finite(double[] a){for(double v:a)if(!Double.isFinite(v))return false;return true;}
    private static boolean rotation(double[] m){
        if(m.length!=9||!finite(m))return false;
        for(int i=0;i<3;i++)for(int j=i;j<3;j++){
            double d=0;for(int k=0;k<3;k++)d+=m[i*3+k]*m[j*3+k];
            if(Math.abs(d-(i==j?1:0))>1e-6)return false;
        }
        double det=m[0]*(m[4]*m[8]-m[5]*m[7])-m[1]*(m[3]*m[8]-m[5]*m[6])+m[2]*(m[3]*m[7]-m[4]*m[6]);
        return Math.abs(det-1)<1e-6;
    }
    private static JSONObject result(boolean valid,String reason,String marker,int features,int matches,int inliers,double median,long start)throws JSONException{
        return new JSONObject().put("valid",valid).put("reason",reason).put("marker",marker)
            .put("features",features).put("matches",matches).put("inliers",inliers).put("median",median)
            .put("position",new JSONArray()).put("forward",new JSONArray()).put("up",new JSONArray())
            .put("milliseconds",(System.nanoTime()-start)/1e6);
    }
    private static boolean dataCell(int x,int y,int n){
        return x>=0&&y>=0&&x<n&&y<n&&!((x<=8&&y<=8)||(x>=n-8&&y<=8)||(x<=8&&y>=n-8)||x==6||y==6);
    }
    private static boolean dataPoint(Point p,Template t){
        double x=(p.x-t.bounds[0])/(t.bounds[2]-t.bounds[0])*t.grid;
        double y=(p.y-t.bounds[1])/(t.bounds[3]-t.bounds[1])*t.grid;
        return x>=0&&y>=0&&x<t.grid&&y<t.grid&&dataCell((int)x,(int)y,t.grid);
    }
    private static Point project(double[] h,double x,double y){
        double z=h[6]*x+h[7]*y+h[8];
        return Math.abs(z)>1e-12?new Point((h[0]*x+h[1]*y+h[2])/z,(h[3]*x+h[4]*y+h[5])/z):new Point(Double.NaN,Double.NaN);
    }
    private static boolean cardGeometry(double[] h,Template t,int width,int height){
        Point[] p={project(h,0,0),project(h,t.width,0),project(h,t.width,t.height),project(h,0,t.height)};
        double area=0,sign=0;
        for(int i=0;i<4;i++){
            Point a=p[i],b=p[(i+1)%4],c=p[(i+2)%4];
            if(!Double.isFinite(a.x)||!Double.isFinite(a.y)||Math.abs(a.x)>width*3||Math.abs(a.y)>height*3||Math.hypot(a.x-b.x,a.y-b.y)<70)return false;
            double cross=(b.x-a.x)*(c.y-b.y)-(b.y-a.y)*(c.x-b.x);
            if(i==0)sign=cross;if(cross*sign<=0)return false;
            area+=a.x*b.y-a.y*b.x;
        }
        // Positive screen winding excludes a mirrored print/camera image.
        return area/2>width*(double)height*.012;
    }
    /** Full-card SIFT homography. Shared patterned border may establish H, but
     * never identity: ten separated payload-region inliers plus bit competition
     * against ALL old A-E codes are mandatory even when QR decoding succeeds.
     */
    private static Evidence evidence(Template t,Mat desc,KeyPoint[] keys,byte[] pixels,int width,int height){
        List<MatOfDMatch> knn=new ArrayList<>();
        MatOfPoint2f src=new MatOfPoint2f(),dst=new MatOfPoint2f();Mat mask=new Mat(),homography=null;
        try{
            matcher.knnMatch(desc,t.desc,knn,2);
            ArrayList<DMatch> good=new ArrayList<>();
            for(MatOfDMatch row:knn){
                DMatch[] pair=row.toArray();
                if(pair.length==2&&pair[0].distance<RATIO*pair[1].distance&&pair[0].distance<=260)good.add(pair[0]);
            }
            Collections.sort(good,new Comparator<DMatch>(){public int compare(DMatch a,DMatch b){return Float.compare(a.distance,b.distance);}});
            HashSet<Long> trainCells=new HashSet<>(),imageCells=new HashSet<>();
            ArrayList<Point> from=new ArrayList<>(),to=new ArrayList<>();
            for(DMatch m:good){
                if(m.trainIdx<0||m.trainIdx>=t.n||m.queryIdx<0||m.queryIdx>=keys.length)continue;
                Point a=new Point(t.xy[m.trainIdx*2],t.xy[m.trainIdx*2+1]),p=keys[m.queryIdx].pt;
                long tc=((long)(a.x/6)<<32)|(long)(a.y/6),ic=((long)(p.x/2)<<32)|(long)(p.y/2);
                if(trainCells.contains(tc)||imageCells.contains(ic))continue;
                trainCells.add(tc);imageCells.add(ic);from.add(a);to.add(p);
                if(from.size()>=600)break;
            }
            if(from.size()<MIN_MATCHES)return null;
            src.fromList(from);dst.fromList(to);
            homography=Calib3d.findHomography(src,dst,Calib3d.RANSAC,3,mask,2000,.995);
            if(homography.empty()||mask.total()!=from.size())return null;
            double[] h=new double[9];homography.get(0,0,h);
            if(!finite(h)||!cardGeometry(h,t,width,height))return null;
            byte[] flags=new byte[from.size()];mask.get(0,0,flags);
            Evidence e=new Evidence();e.template=t;e.matches=from.size();e.h=h;
            HashSet<Integer> qrCells=new HashSet<>();
            double minX=1e9,minY=1e9,maxX=-1e9,maxY=-1e9;
            for(int i=0;i<flags.length;i++)if(flags[i]!=0){
                Point a=from.get(i);e.source.add(a);e.image.add(to.get(i));
                if(dataPoint(a,t)){
                    e.qrInliers++;double x=(a.x-t.bounds[0])/(t.bounds[2]-t.bounds[0]),y=(a.y-t.bounds[1])/(t.bounds[3]-t.bounds[1]);
                    qrCells.add((x>=.5?1:0)+(y>=.5?2:0));
                    minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);
                }
            }
            e.inliers=e.source.size();
            if(e.inliers<MIN_INLIERS||e.inliers<e.matches*.45||e.qrInliers<MIN_QR_INLIERS||qrCells.size()<3||maxX-minX<.35||maxY-minY<.35)return null;
            classifyBits(e,pixels,width,height);
            if(!t.id.equals(e.bitWinner)||e.bitError>MAX_BIT_ERROR||e.bitMargin<MIN_BIT_MARGIN)return null;
            return e;
        }finally{
            for(MatOfDMatch m:knn)release(m);
            for(Mat m:new Mat[]{src,dst,mask,homography})release(m);
        }
    }
    private static double sample(byte[] pixels,int width,int height,Point p){
        if(!Double.isFinite(p.x)||!Double.isFinite(p.y)||p.x<0||p.y<0||p.x>=width-1||p.y>=height-1)return Double.NaN;
        int x=(int)p.x,y=(int)p.y;double dx=p.x-x,dy=p.y-y;int i=y*width+x;
        return ((pixels[i]&255)*(1-dx)+(pixels[i+1]&255)*dx)*(1-dy)+((pixels[i+width]&255)*(1-dx)+(pixels[i+width+1]&255)*dx)*dy;
    }
    private static void classifyBits(Evidence e,byte[] pixels,int width,int height){
        Template t=e.template;ArrayList<Integer> cells=new ArrayList<>();ArrayList<Double> levels=new ArrayList<>();
        for(int y=0;y<t.grid;y++)for(int x=0;x<t.grid;x++)if(dataCell(x,y,t.grid)){
            double[] samples=new double[9];int j=0;
            for(double oy:new double[]{-.18,0,.18})for(double ox:new double[]{-.18,0,.18}){
                double u=t.bounds[0]+(x+.5+ox)*(t.bounds[2]-t.bounds[0])/t.grid;
                double v=t.bounds[1]+(y+.5+oy)*(t.bounds[3]-t.bounds[1])/t.grid;
                samples[j++]=sample(pixels,width,height,project(e.h,u,v));
            }
            if(!finite(samples)){e.bitWinner="";return;}
            Arrays.sort(samples);levels.add(samples[4]);cells.add(y*t.grid+x);
        }
        ArrayList<Double> sorted=new ArrayList<>(levels);Collections.sort(sorted);int n=levels.size();
        double low=sorted.get(n/10),high=sorted.get(n*9/10);
        if(high-low<50){e.bitWinner="";return;}
        double threshold=(low+high)/2;double[] errors=new double[templates.size()];
        for(int k=0;k<templates.size();k++){
            int wrong=0;for(int j=0;j<n;j++)if((levels.get(j)>threshold?1:0)!=templates.get(k).bits[cells.get(j)])wrong++;
            errors[k]=wrong/(double)n;
        }
        int best=0;for(int k=1;k<errors.length;k++)if(errors[k]<errors[best])best=k;
        double second=1;for(int k=0;k<errors.length;k++)if(k!=best)second=Math.min(second,errors[k]);
        e.bitWinner=templates.get(best).id;e.bitError=errors[best];e.bitMargin=second-errors[best];
    }
    private static double[] toUnity(double[] local,boolean position){
        double[] v=new double[3];
        for(int j=0;j<3;j++)v[j]=basis[j*3]*local[0]+basis[j*3+1]*local[1]+basis[j*3+2]*local[2];
        v[2]=-v[2];if(position)for(int j=0;j<3;j++)v[j]+=centre[j];return v;
    }
    private static Pose evaluate(Mat rv,Mat tv,MatOfPoint3f objects,MatOfPoint2f images,Mat K,MatOfDouble distortion){
        Mat rot=new Mat();MatOfPoint2f projected=new MatOfPoint2f();
        try{
            if(rv.total()!=3||tv.total()!=3)return null;
            double[] r=new double[3],t=new double[3];rv.get(0,0,r);tv.get(0,0,t);if(!finite(r)||!finite(t))return null;
            Calib3d.Rodrigues(rv,rot);double[] m=new double[9];rot.get(0,0,m);if(!rotation(m))return null;
            Pose p=new Pose();p.camera=new double[3];p.forward=new double[3];p.up=new double[3];
            for(int j=0;j<3;j++){p.camera[j]=-(m[j]*t[0]+m[j+3]*t[1]+m[j+6]*t[2]);p.forward[j]=m[j+6];p.up[j]=-m[j+3];}
            double distance=Math.sqrt(p.camera[0]*p.camera[0]+p.camera[1]*p.camera[1]+p.camera[2]*p.camera[2]);
            if(p.camera[2]>=-.06||distance<.2||distance>2)return null;
            for(Point3 q:objects.toArray())if(m[6]*q.x+m[7]*q.y+t[2]>.08)p.positive++;
            if(p.positive!=objects.rows())return null;
            Calib3d.projectPoints(objects,rv,tv,K,distortion,projected);
            Point[] a=images.toArray(),b=projected.toArray();double[] errors=new double[a.length];double sum=0;
            if(b.length!=a.length)return null;
            for(int i=0;i<a.length;i++){errors[i]=Math.hypot(a[i].x-b[i].x,a[i].y-b[i].y);sum+=errors[i]*errors[i];}
            if(!finite(errors))return null;Arrays.sort(errors);p.rms=Math.sqrt(sum/errors.length);p.median=errors[errors.length/2];p.p90=errors[(int)(errors.length*.9)];
            return p;
        }finally{release(rot);release(projected);}
    }
    private static double separation(double[] a,double[] b){double d=0;for(int j=0;j<3;j++)d+=(a[j]-b[j])*(a[j]-b[j]);return Math.sqrt(d);}
    private static double angle(double[] a,double[] b){double d=0;for(int j=0;j<3;j++)d+=a[j]*b[j];return Math.toDegrees(Math.acos(Math.max(-1,Math.min(1,d))));}
    private static JSONObject solve(Evidence e,int features,double fx,double fy,double cx,double cy,long start)throws JSONException{
        Mat K=Mat.eye(3,3,CvType.CV_64F);MatOfDouble distortion=new MatOfDouble();
        MatOfPoint3f objects=new MatOfPoint3f();MatOfPoint2f images=new MatOfPoint2f();
        List<Mat> rotations=new ArrayList<>(),translations=new ArrayList<>();
        try{
            K.put(0,0,fx);K.put(1,1,fy);K.put(0,2,cx);K.put(1,2,cy);
            ArrayList<Point3> local=new ArrayList<>();
            for(Point p:e.source)local.add(new Point3((p.x-500)*.0002,(p.y-600)*.0002,0));
            objects.fromList(local);images.fromList(e.image);
            // Both planar solutions are examined. Never turn a homography into
            // a marker-centre camera position, and never snap height or yaw.
            Calib3d.solvePnPGeneric(objects,images,K,distortion,rotations,translations,false,Calib3d.SOLVEPNP_IPPE);
            ArrayList<Pose> poses=new ArrayList<>();
            for(int i=0;i<rotations.size();i++){
                Calib3d.solvePnPRefineLM(objects,images,K,distortion,rotations.get(i),translations.get(i));
                Pose p=evaluate(rotations.get(i),translations.get(i),objects,images,K,distortion);if(p!=null)poses.add(p);
            }
            Collections.sort(poses,new Comparator<Pose>(){public int compare(Pose a,Pose b){return Double.compare(a.rms,b.rms);}});
            if(poses.isEmpty())return result(false,"POSE_NOT_CONSISTENT","A",features,e.matches,e.inliers,-1,start);
            Pose best=poses.get(0);
            if(best.median>2.5||best.p90>4||best.rms>2.5)return result(false,"HIGH_REPROJECTION_ERROR","A",features,e.matches,e.inliers,best.median,start);
            double gap=-1;
            if(poses.size()>1){
                Pose other=poses.get(1);gap=other.rms-best.rms;
                boolean distinct=separation(best.camera,other.camera)>.08||angle(best.forward,other.forward)>8||angle(best.up,other.up)>8;
                if(distinct&&gap<.35&&other.rms<best.rms*1.35+.15)return result(false,"PLANAR_AMBIGUITY","A",features,e.matches,e.inliers,best.median,start).put("poseGap",gap);
            }
            double[] position=toUnity(best.camera,true),forward=toUnity(best.forward,false),up=toUnity(best.up,false);
            if(!finite(position)||!finite(forward)||!finite(up))return result(false,"INVALID_POSE","A",features,e.matches,e.inliers,-1,start);
            // Same existing room bounds as SceneLocalizer; marker has independent
            // planar/identity gates and never weakens natural-scene gates.
            if(position[1]<.45||position[1]>2.65||position[0]<-9.5||position[0]>10||position[2]<-6.5||position[2]>5.5)return result(false,"POSE_OUTSIDE_ROOM","A",features,e.matches,e.inliers,best.median,start);
            return result(true,"MARKER_A_POSE","A",features,e.matches,e.inliers,best.median,start)
                .put("position",new JSONArray(position)).put("forward",new JSONArray(forward)).put("up",new JSONArray(up))
                .put("qrInliers",e.qrInliers).put("qrBitError",e.bitError).put("qrBitMargin",e.bitMargin).put("rms",best.rms).put("poseGap",gap);
        }finally{
            for(Mat m:rotations)release(m);for(Mat m:translations)release(m);
            for(Mat m:new Mat[]{K,distortion,objects,images})release(m);
        }
    }
    public static synchronized String locate(byte[] pixels,int width,int height,double fx,double fy,double cx,double cy){
        long start=System.nanoTime();int features=0;Mat image=null,desc=null,emptyMask=null;MatOfKeyPoint keys=null;
        try{
            if(!ready)return result(false,"DATABASE_NOT_READY","",0,0,0,-1,start).toString();
            if(pixels==null||width<96||height<96||width>MAX_SIDE||height>MAX_SIDE||(long)width*height>MAX_PIXELS||pixels.length!=(long)width*height
                ||!finite(new double[]{fx,fy,cx,cy})||fx<1||fy<1||fx>100000||fy>100000||cx<0||cy<0||cx>=width||cy>=height)return result(false,"INVALID_CAMERA","",0,0,0,-1,start).toString();
            image=new Mat(height,width,CvType.CV_8U);image.put(0,0,pixels);
            if(Core.mean(image).val[0]<22)return result(false,"LOW_LIGHT","",0,0,0,-1,start).toString();
            String decoded=decoder.detectAndDecode(image);
            if(!decoded.isEmpty()&&!PAYLOAD.equals(decoded))return result(false,"WRONG_MARKER",decoded.startsWith("JWM:MAIN:")?decoded.substring(9):"",0,0,0,-1,start).put("payload",decoded).toString();
            desc=new Mat();emptyMask=new Mat();keys=new MatOfKeyPoint();sift.detectAndCompute(image,emptyMask,keys,desc);features=keys.rows();
            if(desc.rows()<MIN_MATCHES)return result(false,"LOW_TEXTURE","",features,0,0,-1,start).toString();
            KeyPoint[] keyArray=keys.toArray();Evidence accepted=null;int contenders=0;
            for(Template t:templates){
                Evidence candidate=evidence(t,desc,keyArray,pixels,width,height);
                if(candidate==null)continue;
                contenders++;if(accepted==null||candidate.qrInliers>accepted.qrInliers)accepted=candidate;
            }
            if(accepted==null)return result(false,"MARKER_IDENTITY_NOT_PROVEN","",features,0,0,-1,start).put("payload",decoded).toString();
            if(contenders>1)return result(false,"MULTIPLE_MARKERS",accepted.template.id,features,accepted.matches,accepted.inliers,-1,start).toString();
            if(!"A".equals(accepted.template.id))return result(false,"WRONG_MARKER",accepted.template.id,features,accepted.matches,accepted.inliers,-1,start).toString();
            JSONObject output=solve(accepted,features,fx,fy,cx,cy,start);
            output.put("payload",decoded).put("identity",decoded.isEmpty()?"TEMPLATE_QR_DATA_A_E":"DECODED_QR_AND_TEMPLATE");
            // Include all work in elapsed time, including the final diagnostics.
            output.put("milliseconds",(System.nanoTime()-start)/1e6);return output.toString();
        }catch(Throwable e){
            try{return result(false,"ENGINE_ERROR:"+e.getClass().getSimpleName(),"",features,0,0,-1,start).toString();}
            catch(Exception ignored){return "{\"valid\":false,\"reason\":\"ENGINE_ERROR\",\"marker\":\"\",\"features\":0,\"matches\":0,\"inliers\":0,\"median\":-1,\"milliseconds\":0,\"position\":[],\"forward\":[],\"up\":[]}";}
        }finally{for(Mat m:new Mat[]{image,desc,emptyMask,keys})release(m);}
    }
}
