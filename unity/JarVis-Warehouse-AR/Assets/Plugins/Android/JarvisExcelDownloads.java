package com.jarvis.warehouse;
import android.app.Activity;
import android.content.ContentValues;
import android.net.Uri;
import android.provider.MediaStore;
import java.io.*;
public final class JarvisExcelDownloads {
    public static String save(Activity activity,String path){
        Uri uri=null;boolean done=false;
        try{
            File source=new File(path).getCanonicalFile();
            if(!source.isFile()||!source.getName().endsWith(".xlsx"))return "ERROR:INVALID_FILE";
            File allowed=activity.getExternalFilesDir(null);
            if(allowed==null||!source.getPath().startsWith(allowed.getCanonicalPath()+File.separator))return "ERROR:OUTSIDE_APP";
            if(android.os.Build.VERSION.SDK_INT<29)return "ERROR:ANDROID_VERSION";
            ContentValues values=new ContentValues();values.put(MediaStore.Downloads.DISPLAY_NAME,source.getName());
            values.put(MediaStore.Downloads.MIME_TYPE,"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            values.put(MediaStore.Downloads.RELATIVE_PATH,"Download/JarVis");values.put(MediaStore.Downloads.IS_PENDING,1);
            uri=activity.getContentResolver().insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI,values);if(uri==null)return "ERROR:CREATE";
            try(InputStream in=new FileInputStream(source);OutputStream out=activity.getContentResolver().openOutputStream(uri)){
                if(out==null)throw new IOException("NO_STREAM");byte[] buffer=new byte[8192];int n;while((n=in.read(buffer))!=-1)out.write(buffer,0,n);
            }
            ContentValues ready=new ContentValues();ready.put(MediaStore.Downloads.IS_PENDING,0);activity.getContentResolver().update(uri,ready,null,null);done=true;return "OK:"+source.getName();
        }catch(Exception e){return "ERROR:"+e.getClass().getSimpleName();}
        finally{if(uri!=null&&!done)activity.getContentResolver().delete(uri,null,null);}
    }
}
