package mono;

import java.io.File;
import java.io.IOException;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;
import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.os.Build;
import android.util.Log;

public final class NativeLibraryHelper {
	static final String TAG = "monodroid";

	private NativeLibraryHelper ()
	{
	}

	public static void loadLibrary (String libraryName, Context context)
	{
		ApplicationInfo applicationInfo = context.getApplicationInfo ();
		String[] splitApks = applicationInfo.splitSourceDirs;
		String[] apks;
		if (splitApks != null && splitApks.length > 0) {
			apks = new String [splitApks.length + 1];
			apks [0] = applicationInfo.sourceDir;
			System.arraycopy (splitApks, 0, apks, 1, splitApks.length);
		} else {
			apks = new String [] { applicationInfo.sourceDir };
		}

		loadLibrary (libraryName, applicationInfo, apks);
	}

	static void loadLibrary (String libraryName, ApplicationInfo applicationInfo, String[] apks)
	{
		try {
			System.loadLibrary (libraryName);
		} catch (UnsatisfiedLinkError cause) {
			String diagnosticMessage = getDiagnosticMessage (libraryName, applicationInfo, apks, cause);
			Log.e (TAG, diagnosticMessage, cause);

			UnsatisfiedLinkError error = new UnsatisfiedLinkError (diagnosticMessage);
			error.initCause (cause);
			throw error;
		} catch (SecurityException cause) {
			String diagnosticMessage = getDiagnosticMessage (libraryName, applicationInfo, apks, cause);
			Log.e (TAG, diagnosticMessage, cause);
			throw new SecurityException (diagnosticMessage, cause);
		}
	}

	static String getDiagnosticMessage (String libraryName, ApplicationInfo applicationInfo, String[] apks, Throwable cause)
	{
		String mappedLibraryName = System.mapLibraryName (libraryName);
		StringBuilder message = new StringBuilder ();
		message.append ("Failed to load native library '").append (mappedLibraryName).append ("'.");
		message.append (" Supported ABIs: ").append (Arrays.toString (Build.SUPPORTED_ABIS)).append (".");

		boolean foundLibrary = appendNativeLibraryDirectoryDiagnostics (message, applicationInfo.nativeLibraryDir, mappedLibraryName);
		foundLibrary |= appendApkDiagnostics (message, apks, mappedLibraryName);

		if (!foundLibrary) {
			message.append (" The library was not found in the native library directory or any application APK that could be inspected.");
			message.append (" The application installation may be corrupt; reinstalling the application may fix this error.");
		}

		String causeMessage = cause.getMessage ();
		if (causeMessage != null && causeMessage.length () > 0) {
			message.append (" Original error: ").append (causeMessage);
		}

		return message.toString ();
	}

	static boolean appendNativeLibraryDirectoryDiagnostics (StringBuilder message, String nativeLibraryDir, String mappedLibraryName)
	{
		message.append (" Native library directory: ");
		if (nativeLibraryDir == null) {
			message.append ("<unknown>.");
			return false;
		}

		File directory = new File (nativeLibraryDir);
		File library = new File (directory, mappedLibraryName);
		boolean libraryExists = library.isFile ();
		message.append ('\'').append (nativeLibraryDir).append ('\'');
		message.append (" (directory exists: ").append (directory.isDirectory ());
		message.append (", library exists: ").append (libraryExists);
		if (libraryExists) {
			message.append (", library size: ").append (library.length ());
			message.append (", library readable: ").append (library.canRead ());
		}
		message.append (").");
		return libraryExists;
	}

	static boolean appendApkDiagnostics (StringBuilder message, String[] apks, String mappedLibraryName)
	{
		boolean foundLibrary = false;
		message.append (" APKs:");
		if (apks == null || apks.length == 0) {
			message.append (" <none>.");
			return false;
		}

		for (String apk : apks) {
			message.append (" '").append (apk).append ("'");
			if (apk == null) {
				message.append (" (invalid path);");
				continue;
			}

			File apkFile = new File (apk);
			if (!apkFile.isFile ()) {
				message.append (" (file exists: false);");
				continue;
			}

			ArrayList<String> entries = new ArrayList<String> ();
			try (ZipFile zip = new ZipFile (apkFile)) {
				for (String abi : Build.SUPPORTED_ABIS) {
					String entryName = "lib/" + abi + "/" + mappedLibraryName;
					ZipEntry entry = zip.getEntry (entryName);
					if (entry == null)
						continue;

					foundLibrary = true;
					String storage = entry.getMethod () == ZipEntry.STORED ? "stored" : "compressed";
					entries.add (abi + ", " + storage + ", size " + entry.getSize ());
				}
				if (entries.size () == 0)
					message.append (" (contains no matching native libraries);");
				else
					message.append (" (contains: ").append (entries).append (");");
			} catch (IOException | SecurityException e) {
				message.append (" (could not inspect: ").append (e.getClass ().getSimpleName ());
				String errorMessage = e.getMessage ();
				if (errorMessage != null && errorMessage.length () > 0)
					message.append (": ").append (errorMessage);
				message.append (");");
			}
		}

		return foundLibrary;
	}
}
