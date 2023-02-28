package mono.android;

import java.lang.Thread;
import java.lang.Throwable;
import android.util.Log;

public class Runtime {
	static java.lang.Class java_lang_Class = java.lang.Class.class;;
	static java.lang.Class java_lang_System = java.lang.System.class;
	static java.lang.Class java_util_TimeZone = java.util.TimeZone.class;
	static java.lang.Class mono_android_IGCUserPeer = mono.android.IGCUserPeer.class;
	static java.lang.Class mono_android_GCUserPeer = mono.android.GCUserPeer.class;

	public static ClassLoader classLoader;

	static {
		Thread.setDefaultUncaughtExceptionHandler (new XamarinUncaughtExceptionHandler (Thread.getDefaultUncaughtExceptionHandler ()));
	}

	public static native void init (String lang, String[] runtimeApks, String runtimeDataDir, String[] appDirs, ClassLoader loader, String[] externalStorageDirs, String[] assemblies, String packageName, int apiLevel, String[] environmentVariables);
	public static native void initInternal (
		String lang,
		String[] runtimeApks,
		String runtimeDataDir,
		String[] appDirs,
		int localDateTimeOffset,
		ClassLoader loader,
		String[] assemblies,
		int apiLevel,
		boolean isEmulator,
		boolean haveSplitApks
	);
	public static native void register (String managedType, java.lang.Class nativeClass, String methods);
	public static native void notifyTimeZoneChanged ();
	public static native int createNewContext (String[] runtimeApks, String[] assemblies, ClassLoader loader);
	public static native int createNewContextWithData (String[] runtimeApks, String[] assemblies, byte[][] assembliesBytes, String[] assembliesPaths, ClassLoader loader, boolean forcePreloadAssemblies);
	public static native void switchToContext (int contextID);
	public static native void destroyContexts (int[] contextIDs);
	public static native void propagateUncaughtException (Thread javaThread, Throwable javaException);
	public static native void dumpTimingData ();

	public static void setCurrentThreadContext ()
	{
		Thread current = Thread.currentThread ();
		current.setContextClassLoader (classLoader);
	}

	public static boolean loadLibrary (String libname)
	{
		try {
			System.loadLibrary (libname);
			return true;
		} catch (java.lang.UnsatisfiedLinkError ex) {
			Log.w ("monodroid", String.format ("Failed to load shared library '%s' with System.loadLibrary", libname), ex);
			return false;
		}
	}
}

final class XamarinUncaughtExceptionHandler implements Thread.UncaughtExceptionHandler {
	Thread.UncaughtExceptionHandler defaultHandler;

	public XamarinUncaughtExceptionHandler (Thread.UncaughtExceptionHandler previousHandler)
	{
		defaultHandler = previousHandler;
	}

	@Override
	public final void uncaughtException (Thread t, Throwable e)
	{
		Runtime.propagateUncaughtException (t, e);

		if (defaultHandler != null)
			defaultHandler.uncaughtException (t, e);
	}
}
