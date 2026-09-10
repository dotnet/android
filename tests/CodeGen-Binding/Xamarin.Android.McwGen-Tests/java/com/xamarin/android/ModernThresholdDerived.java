package com.xamarin.android;

public class ModernThresholdDerived extends LegacyThresholdDerived {
	public boolean modernMethodInvoked;

	@Override
	public void method () {
		modernMethodInvoked = true;
	}
}
