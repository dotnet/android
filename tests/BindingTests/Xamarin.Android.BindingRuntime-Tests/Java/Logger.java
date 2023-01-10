package com.xamarin.android;

// Based of the slf4j-android-1.5.8.jar!org.slf4j.Logger type
public interface Logger {
	// No args; this becomes an IsTraceEnabled property
	// (Whether this is "good" in this scenario is debatable.
	boolean isTraceEnabled ();
	// Has args, but we have a property; this becomes an InvokeIsTraceEnabled()
	// method.
	boolean isTraceEnabled (int ignore);
}
