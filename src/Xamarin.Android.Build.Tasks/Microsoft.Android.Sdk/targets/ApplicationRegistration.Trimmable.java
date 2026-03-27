package net.dot.android;

public class ApplicationRegistration {

	public static android.content.Context Context;

	// In the trimmable typemap path, Application/Instrumentation types are activated
	// via Runtime.registerNatives() + UCO wrappers, not Runtime.register(), so this
	// method is intentionally empty.
	public static void registerApplications ()
	{
	}
}
