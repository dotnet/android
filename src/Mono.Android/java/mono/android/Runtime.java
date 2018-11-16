package mono.android;

public class Runtime {
	static java.lang.Class java_lang_Class = java.lang.Class.class;;
	static java.lang.Class java_lang_System = java.lang.System.class;
	static java.lang.Class java_util_TimeZone = java.util.TimeZone.class;
	static java.lang.Class mono_android_IGCUserPeer = mono.android.IGCUserPeer.class;
	static java.lang.Class mono_android_GCUserPeer = mono.android.GCUserPeer.class;

	private Runtime ()
	{
	}

	public static native void init (String lang, String[] runtimeApks, String runtimeDataDir, String[] appDirs, ClassLoader loader, String[] externalStorageDirs, String[] assemblies, String packageName, int apiLevel, String[] environmentVariables);
	public static native void register (String managedType, java.lang.Class nativeClass, String methods);
	public static native void notifyTimeZoneChanged ();
	public static native int createNewContext (String[] runtimeApks, String[] assemblies, ClassLoader loader);
	public static native void switchToContext (int contextID);
	public static native void destroyContexts (int[] contextIDs);
	public static native void propagateUncaughtException (Thread javaThread, Throwable javaException);
}
