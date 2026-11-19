package net.dot.android;

import android.content.Context;
import android.content.pm.ApplicationInfo;

public class CoreClrAppComponentFactory extends RuntimeAppComponentFactory {
	@Override
	protected void initializeRuntime (ApplicationInfo applicationInfo, ClassLoader loader, Context context)
	{
		CoreClrPackageManager.LoadApplication (applicationInfo, loader, context);
	}
}
