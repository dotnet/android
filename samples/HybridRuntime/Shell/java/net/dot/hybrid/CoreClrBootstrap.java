package net.dot.hybrid;

import android.content.Context;
import android.content.pm.ApplicationInfo;
import android.app.Activity;
import android.os.Build;
import android.os.Handler;
import android.os.HandlerThread;
import android.os.SystemClock;
import android.util.Log;
import java.time.OffsetDateTime;
import java.lang.reflect.Field;
import java.lang.reflect.Method;
import java.util.Calendar;
import java.util.Locale;

final class CoreClrBootstrap {
	private static final String TAG = "HybridRuntime";

	enum State {
		NOT_STARTED,
		INITIALIZING,
		READY,
		FAILED,
	}

	private static volatile State state = State.NOT_STARTED;
	private static volatile Throwable failure;
	private static volatile long initializationDurationMilliseconds;
	private static HandlerThread runtimeThread;

	static synchronized void initializeAsync(Context context) {
		if (state != State.NOT_STARTED) {
			return;
		}

		state = State.INITIALIZING;
		runtimeThread = new HandlerThread("CoreCLR runtime");
		runtimeThread.start();

		Handler runtimeHandler = new Handler(runtimeThread.getLooper());
		Context applicationContext = context.getApplicationContext();
		runtimeHandler.post(() -> {
			long start = SystemClock.elapsedRealtime();
			try {
				initialize(applicationContext);
				initializationDurationMilliseconds = SystemClock.elapsedRealtime() - start;
				state = State.READY;
				Log.i(TAG, "CoreCLR initialized in " + initializationDurationMilliseconds +
					" ms on thread " + Thread.currentThread().getName());
			} catch (Throwable error) {
				initializationDurationMilliseconds = SystemClock.elapsedRealtime() - start;
				failure = error;
				state = State.FAILED;
				Log.e(TAG, "CoreCLR initialization failed after " +
					initializationDurationMilliseconds + " ms", error);
			}
		});
	}

	static State getState() {
		return state;
	}

	static Throwable getFailure() {
		return failure;
	}

	static long getInitializationDurationMilliseconds() {
		return initializationDurationMilliseconds;
	}

	static void showTodoApp(Activity activity) throws Exception {
		Class<?> runtimeClass = Class.forName("mono.android.Runtime", true, activity.getClassLoader());
		Method showTodoApp = runtimeClass.getDeclaredMethod(
			"invokeStaticVoidMethodWithObject",
			String.class,
			String.class,
			String.class,
			Object.class
		);
		showTodoApp.invoke(
			null,
			"HybridRuntimeCoreClr",
			"HybridRuntime.CoreClrPayload.CoreClrPayload",
			"ShowTodoApp",
			activity
		);
	}

	private static void initialize(Context context) throws Exception {
		ApplicationInfo applicationInfo = context.getApplicationInfo();
		Class<?> applicationRegistration = Class.forName(
			"net.dot.android.ApplicationRegistration",
			true,
			context.getClassLoader()
		);
		Field applicationContext = applicationRegistration.getDeclaredField("Context");
		applicationContext.set(null, context.getApplicationContext());
		String[] apks;
		if (applicationInfo.splitSourceDirs != null && applicationInfo.splitSourceDirs.length > 0) {
			apks = new String[applicationInfo.splitSourceDirs.length + 1];
			apks[0] = applicationInfo.sourceDir;
			System.arraycopy(applicationInfo.splitSourceDirs, 0, apks, 1, applicationInfo.splitSourceDirs.length);
		} else {
			apks = new String[] { applicationInfo.sourceDir };
		}

		Locale locale = Locale.getDefault();
		String language = locale.getLanguage() + "-" + locale.getCountry();
		String[] appDirs = new String[] {
			context.getFilesDir().getAbsolutePath(),
			context.getCacheDir().getAbsolutePath(),
			applicationInfo.nativeLibraryDir,
			context.getCodeCacheDir().getAbsolutePath(),
		};

		int localDateTimeOffset;
		if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
			localDateTimeOffset = OffsetDateTime.now().getOffset().getTotalSeconds();
		} else {
			Calendar calendar = Calendar.getInstance();
			localDateTimeOffset = (calendar.get(Calendar.ZONE_OFFSET) + calendar.get(Calendar.DST_OFFSET)) / 1000;
		}

		System.loadLibrary("monodroid");
		Class<?> runtimeClass = Class.forName("mono.android.Runtime", true, context.getClassLoader());
		Method initInternal = runtimeClass.getDeclaredMethod(
			"initInternal",
			String.class,
			String[].class,
			String.class,
			String[].class,
			int.class,
			ClassLoader.class,
			String[].class,
			boolean.class,
			boolean.class
		);
		initInternal.invoke(
			null,
			language,
			apks,
			applicationInfo.nativeLibraryDir,
			appDirs,
			localDateTimeOffset,
			context.getClassLoader(),
			null,
			isEmulator(),
			applicationInfo.splitSourceDirs != null && applicationInfo.splitSourceDirs.length > 0
		);
		Method invokeStaticVoidMethod = runtimeClass.getDeclaredMethod(
			"invokeStaticVoidMethod",
			String.class,
			String.class,
			String.class
		);
		invokeStaticVoidMethod.invoke(
			null,
			"HybridRuntimeCoreClr",
			"HybridRuntime.CoreClrPayload.CoreClrPayload",
			"Warmup"
		);
	}

	private static boolean isEmulator() {
		return Build.HARDWARE.contains("ranchu") || Build.HARDWARE.contains("goldfish");
	}

	private CoreClrBootstrap() {
	}
}
