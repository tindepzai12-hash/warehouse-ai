package com.jarvis.warehouse;

import android.app.Activity;
import android.app.Fragment;
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

// Android document picker grants access only to the image the user explicitly selects.
// No blanket photo/storage permission, gallery package or third-party upload is needed.
public class ChatPhotoPicker extends Fragment {
    // Unity's keyboard area is empty on some Android keyboards. Read actual IME insets.
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
    private String objectName, callback;
    private boolean completed;
    public static void pick(final String objectName, final String callback, final String cachePath) {
        final Activity activity=UnityPlayer.currentActivity;
        activity.runOnUiThread(() -> {
            ChatPhotoPicker fragment=new ChatPhotoPicker();
            Bundle args=new Bundle();args.putString("object",objectName);args.putString("callback",callback);args.putString("cache",cachePath);fragment.setArguments(args);
            activity.getFragmentManager().beginTransaction().add(fragment,"JarVisPhotoPicker").commit();
        });
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);objectName=getArguments().getString("object");callback=getArguments().getString("callback");
        if(state==null) {
            try {Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("image/*");intent.putExtra(Intent.EXTRA_MIME_TYPES,new String[]{"image/jpeg","image/png"});startActivityForResult(intent,701);}
            catch(Exception e){finish("ERROR");}
        }
    }
    @Override public void onActivityResult(int request,int result,Intent data) {
        super.onActivityResult(request,result,data);if(request!=701)return;
        if(result!=Activity.RESULT_OK||data==null){finish("");return;}
        // Decode and compress on a worker; bitmap dimensions are inspected before allocating.
        new Thread(() -> {
            File file=null;Bitmap bitmap=null,scaled=null,oriented=null;
            try {
                Uri uri=data.getData();BitmapFactory.Options options=new BitmapFactory.Options();options.inJustDecodeBounds=true;
                try(InputStream stream=getActivity().getContentResolver().openInputStream(uri)){BitmapFactory.decodeStream(stream,null,options);}
                if(options.outWidth<=0||options.outHeight<=0)throw new IllegalArgumentException();
                int sample=1;while(Math.max(options.outWidth,options.outHeight)/sample>2048)sample*=2;
                options.inJustDecodeBounds=false;options.inSampleSize=sample;
                try(InputStream stream=getActivity().getContentResolver().openInputStream(uri)){bitmap=BitmapFactory.decodeStream(stream,null,options);}
                if(bitmap==null)throw new IllegalArgumentException();
                int orientation=ExifInterface.ORIENTATION_NORMAL;
                try(InputStream stream=getActivity().getContentResolver().openInputStream(uri)){orientation=new ExifInterface(stream).getAttributeInt(ExifInterface.TAG_ORIENTATION,ExifInterface.ORIENTATION_NORMAL);}
                catch(Exception ignored){}
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
                float factor=Math.min(1f,1024f/Math.max(bitmap.getWidth(),bitmap.getHeight()));
                scaled=Bitmap.createScaledBitmap(oriented,Math.max(1,Math.round(oriented.getWidth()*factor)),Math.max(1,Math.round(oriented.getHeight()*factor)),true);
                file=File.createTempFile("jarvis-photo-",".jpg",new File(getArguments().getString("cache")));
                try(FileOutputStream out=new FileOutputStream(file)){if(!scaled.compress(Bitmap.CompressFormat.JPEG,85,out))throw new IllegalArgumentException();}
                if(file.length()>2*1024*1024)throw new IllegalArgumentException();
                finish(file.getAbsolutePath());
            }catch(Exception e){if(file!=null)file.delete();finish("ERROR");}
            finally{if(scaled!=null&&scaled!=oriented&&scaled!=bitmap)scaled.recycle();if(oriented!=null&&oriented!=bitmap)oriented.recycle();if(bitmap!=null)bitmap.recycle();}
        },"JarVisPhoto").start();
    }
    private void finish(final String path) {
        Activity activity=getActivity();if(activity==null){if(!path.isEmpty()&&!path.equals("ERROR"))new File(path).delete();return;}
        activity.runOnUiThread(()->{if(completed)return;completed=true;UnityPlayer.UnitySendMessage(objectName,callback,path);getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss();});
    }
}
