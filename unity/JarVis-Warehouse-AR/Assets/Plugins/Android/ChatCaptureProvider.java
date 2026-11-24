package com.jarvis.warehouse;

import android.content.ContentProvider;
import android.content.ContentValues;
import android.content.Context;
import android.database.Cursor;
import android.database.MatrixCursor;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.OpenableColumns;
import java.io.File;
import java.io.FileNotFoundException;

// Exposes exactly one short-lived file in this app's private cache. The provider
// is non-exported; only the camera activity receiving the explicit URI grant can
// write/read that file. It never exposes arbitrary paths or app user data.
public final class ChatCaptureProvider extends ContentProvider {
    public static final String FILE_NAME="jarvis-camera-capture.jpg";
    private static final String PATH="capture.jpg";
    private static final String SUFFIX=".jarvis-capture";

    public static Uri createUri(Context context){return new Uri.Builder().scheme("content").authority(context.getPackageName()+SUFFIX).appendPath(PATH).build();}
    private boolean valid(Uri uri){Context c=getContext();return c!=null&&uri!=null&&("content://"+c.getPackageName()+SUFFIX+"/"+PATH).equals(uri.toString());}
    private File captureFile() throws FileNotFoundException {
        Context c=getContext();if(c==null)throw new FileNotFoundException("Provider context unavailable");
        try {File root=c.getCacheDir().getCanonicalFile();File file=new File(root,FILE_NAME).getCanonicalFile();if(!root.equals(file.getParentFile()))throw new FileNotFoundException("Invalid capture path");return file;}
        catch(java.io.IOException e){throw new FileNotFoundException("Invalid capture path");}
    }
    @Override public boolean onCreate(){return true;}
    @Override public String getType(Uri uri){return valid(uri)?"image/jpeg":null;}
    @Override public Cursor query(Uri uri,String[] projection,String selection,String[] args,String sort){
        if(!valid(uri))return null;
        String[] columns=projection==null?new String[]{OpenableColumns.DISPLAY_NAME,OpenableColumns.SIZE}:projection;
        MatrixCursor result=new MatrixCursor(columns,1);Object[] values=new Object[columns.length];File f;
        try{f=captureFile();}catch(FileNotFoundException e){return null;}
        for(int i=0;i<columns.length;i++){if(OpenableColumns.DISPLAY_NAME.equals(columns[i]))values[i]=FILE_NAME;else if(OpenableColumns.SIZE.equals(columns[i]))values[i]=f.exists()?f.length():0L;}
        result.addRow(values);return result;
    }
    @Override public ParcelFileDescriptor openFile(Uri uri,String mode) throws FileNotFoundException {
        if(!valid(uri))throw new FileNotFoundException("Unknown capture URI");
        File file=captureFile();int flags;
        if("r".equals(mode))flags=ParcelFileDescriptor.MODE_READ_ONLY;
        else if("w".equals(mode)||"wt".equals(mode)||"rw".equals(mode))flags=ParcelFileDescriptor.MODE_WRITE_ONLY|ParcelFileDescriptor.MODE_CREATE|ParcelFileDescriptor.MODE_TRUNCATE;
        else if("wa".equals(mode))flags=ParcelFileDescriptor.MODE_WRITE_ONLY|ParcelFileDescriptor.MODE_CREATE|ParcelFileDescriptor.MODE_APPEND;
        else throw new FileNotFoundException("Unsupported capture mode");
        return ParcelFileDescriptor.open(file,flags);
    }
    @Override public int delete(Uri uri,String selection,String[] args){if(!valid(uri))return 0;try{return captureFile().delete()?1:0;}catch(FileNotFoundException e){return 0;}}
    @Override public Uri insert(Uri uri,ContentValues values){throw new UnsupportedOperationException("Capture-only provider");}
    @Override public int update(Uri uri,ContentValues values,String selection,String[] args){throw new UnsupportedOperationException("Capture-only provider");}
}
