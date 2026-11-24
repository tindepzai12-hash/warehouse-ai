package com.jarvis.warehouse;

import android.app.Activity;
import android.app.Fragment;
import android.content.Intent;
import android.os.Bundle;
import com.unity3d.player.UnityPlayer;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;

// User-selected document only; no broad storage permission and no extraction.
public class LabMapPicker extends Fragment {
    private boolean finished;
    public static void pick(String objectName,String callback,String cache) {
        Activity activity=UnityPlayer.currentActivity;
        activity.runOnUiThread(()->{
            LabMapPicker picker=new LabMapPicker();Bundle args=new Bundle();
            args.putString("object",objectName);args.putString("callback",callback);args.putString("cache",cache);picker.setArguments(args);
            activity.getFragmentManager().beginTransaction().add(picker,"JarVisLabMapPicker").commit();
        });
    }
    @Override public void onCreate(Bundle state) {
        super.onCreate(state);
        if(state==null)try {
            Intent intent=new Intent(Intent.ACTION_OPEN_DOCUMENT);intent.addCategory(Intent.CATEGORY_OPENABLE);intent.setType("application/zip");
            intent.putExtra(Intent.EXTRA_MIME_TYPES,new String[]{"application/zip","application/x-zip-compressed","application/octet-stream"});startActivityForResult(intent,802);
        }catch(Exception e){finish("ERROR");}
    }
    @Override public void onActivityResult(int request,int result,Intent data) {
        super.onActivityResult(request,result,data);if(request!=802)return;
        if(result!=Activity.RESULT_OK||data==null){finish("");return;}
        final Activity activity=getActivity();
        new Thread(()->{
            File file=null;
            try {
                file=File.createTempFile("lab-map-",".zip",new File(getArguments().getString("cache")));
                try(InputStream in=activity.getContentResolver().openInputStream(data.getData());FileOutputStream out=new FileOutputStream(file)) {
                    byte[] buffer=new byte[8192];int n,total=0;
                    while((n=in.read(buffer))>0){total+=n;if(total>4*1024*1024+65536)throw new IllegalArgumentException("too large");out.write(buffer,0,n);}
                }
                finish(file.getAbsolutePath());
            }catch(Exception e){if(file!=null)file.delete();finish("ERROR");}
        },"JarVisLabMapImport").start();
    }
    private void finish(String result) {
        if(finished)return;finished=true;
        UnityPlayer.UnitySendMessage(getArguments().getString("object"),getArguments().getString("callback"),result);
        Activity activity=getActivity();if(activity!=null)activity.runOnUiThread(()->getFragmentManager().beginTransaction().remove(this).commitAllowingStateLoss());
    }
}
