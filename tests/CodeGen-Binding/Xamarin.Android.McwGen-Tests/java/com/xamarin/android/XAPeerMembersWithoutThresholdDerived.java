package com.xamarin.android;

public class XAPeerMembersWithoutThresholdDerived extends LegacyThresholdDerived {
	public boolean methodInvokedWithoutThreshold;

	@Override
	public void method () {
		methodInvokedWithoutThreshold = true;
	}
}
