package net.dot.hybrid;

import android.app.ActivityManager;
import android.content.Context;
import android.content.res.Configuration;
import android.os.Process;

public final class HybridApplication extends ManagedMauiApplication {
	private Boolean coreClrProcess;

	@Override
	public void onCreate() {
		if (!isCoreClrProcess()) {
			super.onCreate();
		}
	}

	@Override
	public void onConfigurationChanged(Configuration newConfig) {
		if (!isCoreClrProcess()) {
			super.onConfigurationChanged(newConfig);
		}
	}

	@Override
	public void onLowMemory() {
		if (!isCoreClrProcess()) {
			super.onLowMemory();
		}
	}

	@Override
	public void onTrimMemory(int level) {
		if (!isCoreClrProcess()) {
			super.onTrimMemory(level);
		}
	}

	private boolean isCoreClrProcess() {
		if (coreClrProcess != null) {
			return coreClrProcess;
		}

		int currentPid = Process.myPid();
		ActivityManager activityManager =
			(ActivityManager) getSystemService(Context.ACTIVITY_SERVICE);
		for (ActivityManager.RunningAppProcessInfo process : activityManager.getRunningAppProcesses()) {
			if (process.pid == currentPid) {
				coreClrProcess = process.processName.endsWith(":coreclr");
				return coreClrProcess;
			}
		}

		throw new IllegalStateException("Could not identify the current Android process.");
	}
}
