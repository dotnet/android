package com.xamarin.android;

// Based of the slf4j-android-1.5.8.jar!org.slf4j.helpers.MarkerIgnoringBase type

public abstract class MarkerIgnoringBase implements Logger {

	public boolean IsTraceEnabled (int ignore)
	{
		return true;
	}
}
