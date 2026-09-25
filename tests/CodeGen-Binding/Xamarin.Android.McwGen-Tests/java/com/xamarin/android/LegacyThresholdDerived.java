package com.xamarin.android;

public class LegacyThresholdDerived extends LegacyThresholdBase {
	public boolean derivedMethodInvoked;

	@Override
	public void method () {
		derivedMethodInvoked = true;
	}
}
