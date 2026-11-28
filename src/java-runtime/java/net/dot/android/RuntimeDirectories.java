package net.dot.android;

import java.io.File;
import android.content.Context;
import android.content.ContextWrapper;
import android.content.pm.ApplicationInfo;

// Resolves the directories the runtime is initialized with.
//
// AppComponentFactory.instantiateApplication() runs before Android attaches the Application to its
// base Context, so Context.getFilesDir()/getCacheDir() can't be called yet. ContextImpl computes
// them as new File(ApplicationInfo.dataDir, "files"/"cache"), and ApplicationInfo.dataDir already
// points to device-protected storage for defaultToDeviceProtectedStorage apps, so we do the same
// and also create the directories, as the Context methods do.
public class RuntimeDirectories {

	public static String getFilesDir (ApplicationInfo applicationInfo, Context context)
	{
		if (hasBaseContext (context)) {
			return context.getFilesDir ().getAbsolutePath ();
		}
		return ensureDirExists (applicationInfo, "files");
	}

	public static String getCacheDir (ApplicationInfo applicationInfo, Context context)
	{
		if (hasBaseContext (context)) {
			return context.getCacheDir ().getAbsolutePath ();
		}
		return ensureDirExists (applicationInfo, "cache");
	}

	static boolean hasBaseContext (Context context)
	{
		return !(context instanceof ContextWrapper) || ((ContextWrapper) context).getBaseContext () != null;
	}

	static String ensureDirExists (ApplicationInfo applicationInfo, String name)
	{
		File dir = new File (applicationInfo.dataDir, name);
		dir.mkdirs ();
		return dir.getAbsolutePath ();
	}
}
