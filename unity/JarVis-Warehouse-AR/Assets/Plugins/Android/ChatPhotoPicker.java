package com.jarvis.warehouse;

import android.app.Activity;
import android.app.AlertDialog;
import android.app.Fragment;
import android.content.ClipData;
import android.content.ContentResolver;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.graphics.Matrix;
import android.media.ExifInterface;
import android.net.Uri;
import android.os.Bundle;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

// Opens only after the user taps the chat photo control. Gallery access is the
// Android document picker; camera capture uses a temporary MediaStore URI and
// needs neither CAMERA nor broad photo/storage permission.
public class ChatPhotoPicker extends Fragment {
    private static final int PICK_IMAGE=701, CAPTURE_IMAGE=702;
    private String objectName, callback, cachePath;
    private boolean completed;
    private Uri captureUri;

    public static int keyboardHeight() {
        Activity activity=UnityPlayer.currentActivity;
        android.view.View view=activity.getWindow().getDecorView();
        if(android.os.Build.VERSION.SDK_INT>=30) {
            android.view.WindowInsets insets=view.getRootWindowInsets();
            if(insets!=null)return insets.isVisible(android.view.WindowInsets.Type.ime())?insets.getInsets(android.view.WindowInsets.Type.ime()).bottom:0;
        }
        android.graphics.Rect visible=new android.graphics.Rect();view.getWindowVisibleDisplayFrame(visible);
        int height=view.getRootView().getHeight()-visible.bottom;
        return height>view.getRootView().getHeight()/5?height:0;
    }
    public static void pick(final String objectName, final String callback, final String cachePath, final String language) {
        final Activity activity=UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            ChatPhotoPicker fragment=new ChatPhotoPicker();
            Bundle args=new Bundle();args.putString("object",objectName);args.putString("callback",callback);args.putString("cache",cachePath);args.putString("language",language);fragment.setArguments(args);
            activity.getFragmentManager().beginTransaction().add(fragment,"JarVisPhotoPicker").commit();
        });
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);objectName=getArguments().getString("object");callback=getArguments().getString("callback");cachePath=getArguments().getString("cache");
        if(state!=null){String value=state.getString("captureUri");if(value!=null)captureUri=Uri.parse(value);return;}
        showSourceDialog();
    }
    @Override public void onSaveInstanceState(Bundle state){super.onSaveInstanceState(state);if(captureUri!=null)state.putString("captureUri",captureUri.toString());}

    private void showSourceDialog(){
        Activity activity=getActivity();if(activity==null){finish("");return;}
        boolean english="en".equalsIgnoreCase(getArguments().getString("language","vi"));
        new AlertDialog.Builder(activity)
            .setTitle(english?"Add a photo":"Thêm ảnh")
            .setItems(english?new String[]{"Take a photo","Choose an existing photo"}:new String[]{"Chụp ảnh","Chọn ảnh có sẵn"},(dialog,index)->{if(index==0)openCamera();else openGallery();})
            .setNegativeButton(english?"Cancel":"Hủy",(dialog,index)->finish(""))
            .setOnCancelListener(dialog->finish(""))
            .show();
    }
    private void openGallery(){
        try {Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("image/*");intent.putExtra(Intent.EXTRA_MIME_TYPES,new String[]{"image/jpeg","image/png"});startActivityForResult(intent,PICK_IMAGE);}
        catch(Exception e){finish("ERROR");}
    }
    private void openCamera(){
        try {
            Activity activity=getActivity();if(activity==null)throw new IllegalStateException("Activity unavailable");
            File output=new File(activity.getCacheDir(),ChatCaptureProvider.FILE_NAME);if(output.exists()&&!output.delete())throw new IllegalStateException("Cannot reset temporary capture");
            captureUri=ChatCaptureProvider.createUri(activity);
            Intent intent=new Intent("android.media.action.IMAGE_CAPTURE");intent.putExtra("output",captureUri);intent.setClipData(ClipData.newUri(activity.getContentResolver(),"JarVis camera capture",captureUri));intent.addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION|Intent.FLAG_GRANT_WRITE_URI_PERMISSION);
            startActivityForResult(intent,CAPTURE_IMAGE);
        } catch(Exception e){deleteCapture();finish("ERROR");}
    }
    @Override public void onActivityResult(int request,int result,Intent data) {
        super.onActivityResult(request,result,data);
        if(request!=PICK_IMAGE&&request!=CAPTURE_IMAGE)return;
        Uri uri=request==CAPTURE_IMAGE?captureUri:(data==null?null:data.getData());
        if(result!=Activity.RESULT_OK||uri==null){deleteCapture();finish("");return;}
        final Uri selected=uri;final boolean temporaryCapture=request==CAPTURE_IMAGE;final ContentResolver resolver=getActivity().getContentResolver();
        new Thread(() -> processImage(resolver,selected,temporaryCapture),"JarVisPhoto").start();
    }
    private void processImage(ContentResolver resolver,Uri uri,boolean temporaryCapture){
        File file=null;Bitmap bitmap=null,scaled=null,oriented=null;
        try {
            File cache=new File(cachePath);if(!cache.isDirectory()&&!cache.mkdirs())throw new IllegalStateException("Cache unavailable");
            BitmapFactory.Options options=new BitmapFactory.Options();options.inJustDecodeBounds=true;
            try(InputStream stream=resolver.openInputStream(uri)){if(stream==null)throw new IllegalArgumentException();BitmapFactory.decodeStream(stream,null,options);}
            if(options.outWidth<=0||options.outHeight<=0)throw new IllegalArgumentException();
            int sample=1;while(Math.max(options.outWidth,options.outHeight)/sample>2048)sample*=2;
            options.inJustDecodeBounds=false;options.inSampleSize=sample;
            try(InputStream stream=resolver.openInputStream(uri)){if(stream==null)throw new IllegalArgumentException();bitmap=BitmapFactory.decodeStream(stream,null,options);}
            if(bitmap==null)throw new IllegalArgumentException();
            int orientation=ExifInterface.ORIENTATION_NORMAL;
            try(InputStream stream=resolver.openInputStream(uri)){if(stream!=null)orientation=new ExifInterface(stream).getAttributeInt(ExifInterface.TAG_ORIENTATION,ExifInterface.ORIENTATION_NORMAL);}catch(Exception ignored){}
            Matrix matrix=new Matrix();
            switch(orientation){
                case ExifInterface.ORIENTATION_FLIP_HORIZONTAL:matrix.setScale(-1,1);break;
                case ExifInterface.ORIENTATION_ROTATE_180:matrix.setRotate(180);break;
                case ExifInterface.ORIENTATION_FLIP_VERTICAL:matrix.setScale(1,-1);break;
                case ExifInterface.ORIENTATION_TRANSPOSE:matrix.setRotate(90);matrix.postScale(-1,1);break;
                case ExifInterface.ORIENTATION_ROTATE_90:matrix.setRotate(90);break;
                case ExifInterface.ORIENTATION_TRANSVERSE:matrix.setRotate(-90);matrix.postScale(-1,1);break;
                case ExifInterface.ORIENTATION_ROTATE_270:matrix.setRotate(-90);break;
            }
            oriented=Bitmap.createBitmap(bitmap,0,0,bitmap.getWidth(),bitmap.getHeight(),matrix,true);
            float factor=Math.min(1f,1024f/Math.max(oriented.getWidth(),oriented.getHeight()));
            scaled=Bitmap.createScaledBitmap(oriented,Math.max(1,Math.round(oriented.getWidth()*factor)),Math.max(1,Math.round(oriented.getHeight()*factor)),true);
            file=File.createTempFile("jarvis-photo-",".jpg",cache);
            try(FileOutputStream out=new FileOutputStream(file)){if(!scaled.compress(Bitmap.CompressFormat.JPEG,85,out))throw new IllegalArgumentException();}
            if(file.length()>2*1024*1024)throw new IllegalArgumentException();
            finish(file.getAbsolutePath());
        } catch(Exception e){if(file!=null)file.delete();finish("ERROR");}
        finally {
            if(scaled!=null&&scaled!=oriented&&scaled!=bitmap)scaled.recycle();if(oriented!=null&&oriented!=bitmap)oriented.recycle();if(bitmap!=null)bitmap.recycle();
            if(temporaryCapture)deleteCapture(resolver,uri);
        }
    }
    private void deleteCapture(){if(captureUri!=null)deleteCapture(getActivity()==null?null:getActivity().getContentResolver(),captureUri);}
    private void deleteCapture(ContentResolver resolver,Uri uri){if(resolver!=null&&uri!=null)try{resolver.delete(uri,null,null);}catch(Exception ignored){}captureUri=null;}
    private void finish(final String path) {
        Activity activity=getActivity();if(activity==null){if(!path.isEmpty()&&!path.equals("ERROR"))new File(path).delete();return;}
        activity.runOnUiThread(()->{if(completed)return;completed=true;UnityPlayer.UnitySendMessage(objectName,callback,path);getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();});
    }
}
