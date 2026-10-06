package net.dot.android;

import android.content.Context;
import android.util.Log;
import mono.NativeLibraryHelper;

public final class AppBootstrapConfig
{
	private static final String TAG = "AppBootstrapConfig";
@FIELDS@

	private AppBootstrapConfig ()
	{
	}

	public static void preloadJniLibraries ()
	{
		if (NativeLibraries.length != NativeLibraryFlags.length) {
			throw new IllegalStateException ("Invalid native library preload configuration");
		}
		for (int i = 0; i < NativeLibraries.length; i++) {
			if ((NativeLibraryFlags [i] & 2) == 0) {
				continue;
			}
			try {
				loadJniLibrary (NativeLibraries [i]);
			} catch (UnsatisfiedLinkError | SecurityException e) {
				Log.e (TAG, "Unable to preload JNI library " + NativeLibraries [i], e);
			}
		}
	}

	public static void loadJniLibrary (String fileName)
	{
		Context startupContext = ApplicationRegistration.Context;
		if (startupContext == null) {
			throw new IllegalStateException ("Application context has not been set");
		}
		if (fileName == null || !fileName.startsWith ("lib") || !fileName.endsWith (".so") || fileName.length () <= 5) {
			throw new IllegalArgumentException ("Invalid JNI library name: " + fileName);
		}
		NativeLibraryHelper.loadLibrary (fileName.substring (3, fileName.length () - 3), startupContext);
	}
}
