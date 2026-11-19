package net.dot.android;

import java.io.*;
import java.lang.String;
import java.time.OffsetDateTime;
import java.util.Calendar;
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
import mono.MonoPackageManager_Resources;
import mono.NativeLibraryHelper;
import mono.android.Runtime;

public class CoreClrPackageManager {

	static Object lock = new Object ();
	static boolean initialized;

	public static void LoadApplication (Context context)
	{
		synchronized (lock) {
			if (initialized) {
				if (context instanceof android.app.Application) {
					ApplicationRegistration.Context = context;
				}
				return;
			}
			LoadApplication (
				context.getApplicationInfo (),
				context.getClassLoader (),
				context
			);
		}
	}

	// Shared by MonoRuntimeProvider (API < 29) and CoreClrAppComponentFactory (API 29+); must not reference AppComponentFactory types.
	public static void LoadApplication (ApplicationInfo runtimePackage, ClassLoader loader, Context context)
	{
		synchronized (lock) {
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
				String dataDir      = getNativeLibraryPath (runtimePackage);
				int localDateTimeOffset;

				if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.O) {
					localDateTimeOffset = OffsetDateTime.now().getOffset().getTotalSeconds();
				} else {
					localDateTimeOffset = (Calendar.getInstance ().get (Calendar.ZONE_OFFSET) + Calendar.getInstance ().get (Calendar.DST_OFFSET)) / 1000;
				}

				//
				// Should the order change here, src/native/clr/include/constants.hh must be updated accordingly
				//
				String[] appDirs = new String[] {
					RuntimeDirectories.getFilesDir (runtimePackage, context),
					RuntimeDirectories.getCacheDir (runtimePackage, context),
					dataDir
				};
				boolean haveSplitApks = runtimePackage.splitSourceDirs != null && runtimePackage.splitSourceDirs.length > 0;

				NativeLibraryHelper.loadLibrary ("monodroid", runtimePackage, apks);

				Runtime.initInternal (
					language,
					apks,
					appDirs,
					localDateTimeOffset,
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
