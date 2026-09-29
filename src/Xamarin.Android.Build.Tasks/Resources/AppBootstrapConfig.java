package net.dot.android;

import android.content.Context;
import android.system.ErrnoException;
import android.system.Os;
import android.util.Log;
import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.util.Locale;
import mono.NativeLibraryHelper;

public final class AppBootstrapConfig
{
	private static final String TAG = "AppBootstrapConfig";
	private static Context startupContext;

@FIELDS@

	private AppBootstrapConfig ()
	{
	}

	public static void applyEnvironment (Context context)
	{
		if (context == null) {
			throw new IllegalArgumentException ("Application context is required");
		}

		startupContext = context;
		File filesDir = context.getFilesDir ();
		File cacheDir = context.getCacheDir ();
		File xdgData = new File (filesDir, ".local/share");
		File xdgConfig = new File (filesDir, ".config");
		createDirectory (xdgData);
		createDirectory (xdgConfig);

		Locale locale = Locale.getDefault ();
		try {
			Os.setenv ("LANG", locale.getLanguage () + "-" + locale.getCountry (), true);
			Os.setenv ("TMPDIR", cacheDir.getAbsolutePath (), true);
			Os.setenv ("HOME", filesDir.getAbsolutePath (), true);
			Os.setenv ("XDG_DATA_HOME", xdgData.getAbsolutePath (), true);
			Os.setenv ("XDG_CONFIG_HOME", xdgConfig.getAbsolutePath (), true);

			if ((Environment.length & 1) != 0) {
				throw new IllegalStateException ("Invalid application environment configuration");
			}
			for (int i = 0; i < Environment.length; i += 2) {
				Os.setenv (Environment[i], Environment[i + 1], true);
			}
			Os.setenv ("DOTNET_CrashReportRootPath", cacheDir.getAbsolutePath (), false);
		} catch (ErrnoException e) {
			throw new IllegalStateException ("Unable to configure the application environment", e);
		}
	}

	private static void createDirectory (File directory)
	{
		if (!directory.isDirectory () && !directory.mkdirs () && !directory.isDirectory ()) {
			throw new IllegalStateException ("Unable to create directory " + directory);
		}
	}

	public static byte[] readRemappingAsset (String runtimeIdentifier)
	{
		if (startupContext == null) {
			throw new IllegalStateException ("Application environment has not been initialized");
		}
		if (runtimeIdentifier == null || runtimeIdentifier.isEmpty ()) {
			throw new IllegalArgumentException ("Runtime identifier is required");
		}

		String assetName = "xa-internal/jni-remap." + runtimeIdentifier + ".bin";
		try (InputStream input = startupContext.getAssets ().open (assetName)) {
			ByteArrayOutputStream output = new ByteArrayOutputStream (Math.max (input.available (), 32));
			byte[] buffer = new byte[4096];
			int count;
			while ((count = input.read (buffer)) != -1) {
				output.write (buffer, 0, count);
			}
			return output.toByteArray ();
		} catch (IOException e) {
			throw new IllegalStateException ("Unable to read JNI remapping asset " + assetName, e);
		}
	}

	public static void preloadJniLibraries ()
	{
		if (NativeLibraries.length != NativeLibraryFlags.length) {
			throw new IllegalStateException ("Invalid native library preload configuration");
		}

		for (int i = 0; i < NativeLibraries.length; i++) {
			if ((NativeLibraryFlags[i] & 2) == 0) {
				continue;
			}
			try {
				loadJniLibrary (NativeLibraries[i]);
			} catch (UnsatisfiedLinkError | SecurityException e) {
				Log.e (TAG, "Unable to preload JNI library " + NativeLibraries[i], e);
			}
		}
	}

	public static void loadJniLibrary (String fileName)
	{
		if (startupContext == null) {
			throw new IllegalStateException ("Application environment has not been initialized");
		}
		if (fileName == null || !fileName.startsWith ("lib") || !fileName.endsWith (".so") || fileName.length () <= 5) {
			throw new IllegalArgumentException ("Invalid JNI library name: " + fileName);
		}
		String name = fileName.substring (3, fileName.length () - 3);
		NativeLibraryHelper.loadLibrary (name, startupContext);
	}
}
