package mono.android;

import java.lang.Thread;
import java.lang.Throwable;

public class Runtime {
	static java.lang.Class java_lang_System = java.lang.System.class;
	static java.lang.Class mono_android_IGCUserPeer = mono.android.IGCUserPeer.class;

	static {
		Thread.setDefaultUncaughtExceptionHandler (new XamarinUncaughtExceptionHandler (Thread.getDefaultUncaughtExceptionHandler ()));
	}

	public static native void initInternal (
		String lang,
		String[] runtimeApks,
		String runtimeDataDir,
		String[] appDirs,
		int localDateTimeOffset,
		ClassLoader loader,
		String[] assemblies,
		boolean isEmulator,
		boolean haveSplitApks
	);
	public static native void registerNatives (java.lang.Class nativeClass);
	public static native void propagateUncaughtException (Thread javaThread, Throwable javaException);
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
