package mono;

import java.io.*;
import java.lang.String;
import java.util.Locale;
import java.util.HashSet;
import java.util.zip.*;
import java.util.Arrays;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.res.AssetManager;
import android.os.Build;
import android.util.Log;
import mono.android.Runtime;
import net.dot.android.ApplicationRegistration;

public class MonoPackageManager {

	static Object lock = new Object ();
	static boolean initialized;

	public static void LoadApplication (Context context)
	{
		synchronized (lock) {
			android.content.pm.ApplicationInfo runtimePackage = context.getApplicationInfo ();
			String[] apks = null;
			String[] splitApks = runtimePackage.splitSourceDirs;
			if (splitApks != null && splitApks.length > 0) {
				apks = new String[splitApks.length + 1];
				apks [0] = runtimePackage.sourceDir;
				System.arraycopy (splitApks, 0, apks, 1, splitApks.length);
			} else {
				apks = new String[] { runtimePackage.sourceDir };
			}

			if (context instanceof android.app.Application) {
				ApplicationRegistration.Context = context;
			}
			if (!initialized) {
				Locale locale       = Locale.getDefault ();
				String language     = locale.getLanguage () + "-" + locale.getCountry ();
				String filesDir     = context.getFilesDir ().getAbsolutePath ();
				String cacheDir     = context.getCacheDir ().getAbsolutePath ();
				String dataDir      = getNativeLibraryPath (context);
				ClassLoader loader  = context.getClassLoader ();

				//
				// Should the order change here, src/native/clr/include/constants.hh must be updated accordingly
				//
				String[] appDirs = new String[] {filesDir, cacheDir, dataDir};
				boolean haveSplitApks = runtimePackage.splitSourceDirs != null && runtimePackage.splitSourceDirs.length > 0;

				NativeLibraryHelper.loadLibrary ("monodroid", runtimePackage, apks);

				Runtime.initInternal (
					language,
					apks,
					appDirs,
					loader,
					isEmulator (),
					haveSplitApks
				);

				ApplicationRegistration.registerApplications ();
				initialized = true;
			}
		}
	}

	// We need to detect the emulator in order to determine the maximum gref count.
	// The official Android emulator requires a much lower maximum than actual
	// devices. Hopefully other emulators don't need the treatment. If they do, we
	// can add their detection here. We should perform the absolute minimum of
	// checking in order to save time.
	static boolean isEmulator()
	{
		String val = Build.HARDWARE;

		// This detects the official Android emulator
		if (val.contains ("ranchu") || val.contains ("goldfish"))
			return true;

		return false;
	}

	static String getNativeLibraryPath (Context context)
	{
		return getNativeLibraryPath (context.getApplicationInfo ());
	}

	static String getNativeLibraryPath (ApplicationInfo ainfo)
	{
		return ainfo.nativeLibraryDir;
	}

	public static String[] getAssemblies ()
	{
		return MonoPackageManager_Resources.Assemblies;
	}

}
