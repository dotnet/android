package net.dot.android;

import android.app.AppComponentFactory;
import android.app.Application;
import android.content.Context;
import android.content.pm.ApplicationInfo;

// Initializes .NET before Android creates the Application. Unlike the runtime ContentProvider,
// this also runs in restricted backup processes, where Android skips all providers.
//
// Only effective on API 29+: instantiateClassLoader(), the sole hook that provides the ApplicationInfo
// before the Application exists, was added in API 29. On API 28 the factory is instantiated but does
// nothing, and the runtime ContentProvider initializes .NET, as it does on API < 28.
public abstract class RuntimeAppComponentFactory extends AppComponentFactory {
	private ApplicationInfo applicationInfo;

	@Override
	public ClassLoader instantiateClassLoader (ClassLoader cl, ApplicationInfo aInfo)
	{
		applicationInfo = aInfo;
		return super.instantiateClassLoader (cl, aInfo);
	}

	@Override
	public Application instantiateApplication (ClassLoader cl, String className)
		throws InstantiationException, IllegalAccessException, ClassNotFoundException
	{
		Application application = super.instantiateApplication (cl, className);
		// null on API 28: instantiateClassLoader() is API 29+, so it never ran. Don't throw; the runtime
		// ContentProvider initializes .NET instead.
		if (applicationInfo != null) {
			initializeRuntime (applicationInfo, cl, application);
		}
		return application;
	}

	protected abstract void initializeRuntime (ApplicationInfo applicationInfo, ClassLoader loader, Context context);
}
