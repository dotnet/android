package mono;

import java.io.*;
import java.lang.String;
import java.time.OffsetDateTime;
import java.time.ZoneOffset;
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
import mono.android.Runtime;
import mono.android.DebugRuntime;
import mono.android.BuildConfig;

public class MonoPackageManager {

	static Object lock = new Object ();
	static boolean initialized;

	static android.content.Context Context;

	public static void LoadApplication (Context context)
	{
		Log.w ("XACLR", "MonoPackageManager.LoadApplication: start");
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
				Context = context;
			}
			if (!initialized) {
				Log.w ("XACLR", "MonoPackageManager.LoadApplication: initializing");
				android.content.IntentFilter timezoneChangedFilter  = new android.content.IntentFilter (
					android.content.Intent.ACTION_TIMEZONE_CHANGED
				);
				context.registerReceiver (new mono.android.app.NotifyTimeZoneChanges (), timezoneChangedFilter);

				Locale locale       = Locale.getDefault ();
				String language     = locale.getLanguage () + "-" + locale.getCountry ();
				String filesDir     = context.getFilesDir ().getAbsolutePath ();
				String cacheDir     = context.getCacheDir ().getAbsolutePath ();
				String dataDir      = getNativeLibraryPath (context);
				ClassLoader loader  = context.getClassLoader ();
				String runtimeDir   = getNativeLibraryPath (runtimePackage);
				int localDateTimeOffset;

				if (android.os.Build.VERSION.SDK_INT >= android.os.Build.VERSION_CODES.O) {
					localDateTimeOffset = OffsetDateTime.now().getOffset().getTotalSeconds();
				} else {
					localDateTimeOffset = (Calendar.getInstance ().get (Calendar.ZONE_OFFSET) + Calendar.getInstance ().get (Calendar.DST_OFFSET)) / 1000;
				}

				//
				// Should the order change here, src/native/clr/include/constants.hh must be updated accordingly
				//
				String[] appDirs = new String[] {filesDir, cacheDir, dataDir};
				boolean haveSplitApks = runtimePackage.splitSourceDirs != null && runtimePackage.splitSourceDirs.length > 0;

				Log.w ("XACLR", "MonoPackageManager.LoadApplication: load monodroid");
				System.loadLibrary("monodroid");

				Log.w ("XACLR", "MonoPackageManager.LoadApplication: call Runtime.initInternal");
				Runtime.initInternal (
					language,
					apks,
					runtimeDir,
					appDirs,
					localDateTimeOffset,
					loader,
					MonoPackageManager_Resources.Assemblies,
					isEmulator (),
					haveSplitApks
				);

				Log.w ("XACLR", "MonoPackageManager.LoadApplication: call registerApplications");
				net.dot.android.ApplicationRegistration.registerApplications ();

				Log.w ("XACLR", "MonoPackageManager.LoadApplication: initialized");
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

	public static void setContext (Context context)
	{
		// Ignore; vestigial
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

	public static String[] getDependencies ()
	{
		return MonoPackageManager_Resources.Dependencies;
	}
}
