package com.teep.atlasar;

import android.app.Activity;
import android.content.ContentResolver;
import android.content.ContentValues;
import android.net.Uri;
import android.os.ParcelFileDescriptor;
import android.provider.MediaStore;
import android.system.Os;
import android.system.OsConstants;
import java.io.*;
import java.util.*;
import java.util.zip.*;

public final class AtlasExport {
    private static final int MAX_DEBUG_IMAGES=12,MAX_FILES=4096,MAX_DEPTH=32;

    /** Legacy file copy, or recursively ZIP exactly one caller-supplied session directory.
     * The caller serializes diagnostics writes with export. Only explicitly saved JPEGs are included,
     * up to 12, in deterministic relative-name order. No camera access or external path discovery.
     * Returns OK:<filename> after publication, otherwise ERROR:<reason>.
     */
    public static String save(Activity activity,String path){
        Uri uri=null;boolean published=false;ContentResolver resolver=null;
        try{
            if(activity==null||path==null||path.trim().isEmpty())return "ERROR:INVALID_ARGUMENT";
            File supplied=new File(path).getAbsoluteFile();
            if(!supplied.exists())return "ERROR:MISSING_LOG";
            rejectLink(supplied);
            File source=supplied.getCanonicalFile();boolean directory=source.isDirectory();
            if(!directory&&!source.isFile())return "ERROR:MISSING_LOG";
            String filename=source.getName()+(directory?".zip":"");
            if(filename.isEmpty()||filename.indexOf('\\')>=0)throw new IOException("INVALID_NAME");
            ArrayList<File> files=new ArrayList<>();
            if(directory){
                collect(source,source,files,new HashSet<String>(),0);
                Collections.sort(files,new Comparator<File>(){public int compare(File a,File b){return a.getPath().compareTo(b.getPath());}});
                if(files.isEmpty())return "ERROR:EMPTY_SESSION";
            }
            resolver=activity.getContentResolver();
            ContentValues values=new ContentValues();
            values.put(MediaStore.Downloads.DISPLAY_NAME,filename);
            values.put(MediaStore.Downloads.MIME_TYPE,directory?"application/zip":"application/x-ndjson");
            values.put(MediaStore.Downloads.RELATIVE_PATH,"Download/AtlasAR");values.put(MediaStore.Downloads.IS_PENDING,1);
            uri=resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI,values);if(uri==null)return "ERROR:CREATE";
            try(OutputStream out=resolver.openOutputStream(uri)){
                if(out==null)throw new IOException("NO_OUTPUT_STREAM");
                if(directory){
                    try(ZipOutputStream zip=new ZipOutputStream(new BufferedOutputStream(out))){
                        byte[] buffer=new byte[8192];int debugImages=0;
                        for(File file:files){
                            String entry=relativeName(source,file);
                            String lower=entry.toLowerCase(Locale.ROOT);
                            if(lower.endsWith(".jpg")||lower.endsWith(".jpeg")){
                                if(debugImages>=MAX_DEBUG_IMAGES)continue;
                                debugImages++;
                            }
                            // Recheck immediately before opening; never follow a link outside the session.
                            requireInside(source,file);rejectLink(file);
                            zip.putNextEntry(new ZipEntry(entry));copy(source,file,zip,buffer);zip.closeEntry();
                        }
                    }
                }else{rejectLink(supplied);copy(null,source,out,new byte[8192]);}
            }
            values.clear();values.put(MediaStore.Downloads.IS_PENDING,0);
            if(resolver.update(uri,values,null,null)!=1)throw new IOException("PUBLISH_FAILED");
            published=true;return "OK:"+filename;
        }catch(Exception e){return "ERROR:"+e.getClass().getSimpleName()+":"+e.getMessage();}
        finally{
            // Includes null output streams, failed ZIP finish/close and failed pending publication.
            if(!published&&uri!=null&&resolver!=null)try{resolver.delete(uri,null,null);}catch(Exception ignored){}
        }
    }

    private static void rejectLink(File file) throws Exception {
        if(OsConstants.S_ISLNK(Os.lstat(file.getPath()).st_mode))throw new IOException("SYMLINK_NOT_ALLOWED");
    }
    private static void requireInside(File root,File file) throws IOException {
        String prefix=root.getPath()+File.separator;
        if(!file.getCanonicalPath().startsWith(prefix))throw new IOException("OUTSIDE_SESSION");
    }
    private static String relativeName(File root,File file) throws IOException {
        requireInside(root,file);
        String name=file.getCanonicalPath().substring(root.getPath().length()+1).replace(File.separatorChar,'/');
        for(String part:name.split("/"))if(part.isEmpty()||part.equals(".")||part.equals("..")||part.indexOf('\\')>=0||part.indexOf(':')>=0)throw new IOException("UNSAFE_ENTRY_NAME");
        return name;
    }
    private static void collect(File root,File directory,List<File> files,Set<String> visited,int depth) throws Exception {
        if(depth>MAX_DEPTH)throw new IOException("SESSION_TOO_DEEP");
        if(visited.size()>=MAX_FILES)throw new IOException("TOO_MANY_DIRECTORIES");
        rejectLink(directory);
        if(!directory.equals(root))requireInside(root,directory);
        if(!visited.add(directory.getCanonicalPath()))throw new IOException("DIRECTORY_CYCLE");
        File[] children=directory.listFiles();if(children==null)throw new IOException("READ_DIRECTORY");
        for(File child:children){
            rejectLink(child);requireInside(root,child);
            if(child.isDirectory())collect(root,child,files,visited,depth+1);
            else if(child.isFile()){
                if(files.size()>=MAX_FILES)throw new IOException("TOO_MANY_FILES");
                relativeName(root,child);files.add(child);
            }else throw new IOException("UNSUPPORTED_FILE");
        }
    }
    private static void copy(File root,File file,OutputStream out,byte[] buffer) throws Exception {
        // O_NOFOLLOW rejects a final-component symlink; checking the opened descriptor also
        // catches an ancestor replaced by a symlink between traversal and open. O_NONBLOCK
        // prevents a substituted FIFO from blocking before fstat can reject it.
        FileDescriptor fd=Os.open(file.getPath(),OsConstants.O_RDONLY|OsConstants.O_CLOEXEC|OsConstants.O_NOFOLLOW|OsConstants.O_NONBLOCK,0);
        try {
            if(!OsConstants.S_ISREG(Os.fstat(fd).st_mode))throw new IOException("UNSUPPORTED_FILE");
            try(ParcelFileDescriptor owned=ParcelFileDescriptor.dup(fd);
                InputStream in=new ParcelFileDescriptor.AutoCloseInputStream(owned)){
                String opened=Os.readlink("/proc/self/fd/"+owned.getFd());
                if(!opened.equals(file.getCanonicalPath())||(root!=null&&!opened.startsWith(root.getPath()+File.separator)))throw new IOException("SOURCE_CHANGED");
                int n;while((n=in.read(buffer))!=-1)out.write(buffer,0,n);
            }
        }finally{Os.close(fd);}
    }
}
