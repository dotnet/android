package com.xamarin.android;

public class LegacyThresholdThrowable extends Throwable {
	public boolean methodInvoked;

	public void method () {
		methodInvoked = true;
	}
}
