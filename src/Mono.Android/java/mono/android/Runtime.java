package mono.android;

public class Runtime {

	private Runtime ()
	{
	}

	public static native void init (String lang, String[] runtimeApks, String runtimeDataDir, String[] appDirs, ClassLoader loader, String externalStorageDir, String[] assemblies, String packageName);
	public static native void register (String managedType, java.lang.Class nativeClass, String methods);
	public static native void notifyTimeZoneChanged ();
	public static native int createNewContext (String[] runtimeApks, String[] assemblies, ClassLoader loader);
	public static native void switchToContext (int contextID);
	public static native void destroyContexts (int[] contextIDs);
	public static native void propagateUncaughtException (Thread javaThread, Throwable javaException);
}
