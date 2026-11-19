package net.dot.android;

import android.content.Context;
import android.content.pm.ApplicationInfo;
import mono.MonoPackageManager;

public class CoreClrAppComponentFactory extends RuntimeAppComponentFactory {
	@Override
	protected void initializeRuntime (ApplicationInfo applicationInfo, ClassLoader loader, Context context)
	{
		MonoPackageManager.LoadApplication (applicationInfo, loader, context);
	}
}
