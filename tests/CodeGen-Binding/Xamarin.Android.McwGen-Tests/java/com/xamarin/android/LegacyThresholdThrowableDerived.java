package com.xamarin.android;

public class LegacyThresholdThrowableDerived extends LegacyThresholdThrowable {
	public boolean derivedMethodInvoked;

	@Override
	public void method () {
		derivedMethodInvoked = true;
	}
}
