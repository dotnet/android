package com.xamarin.android;

public class CallNonvirtualBase {

	public boolean methodInvoked;
	public void method () {
		System.out.println ("CallNonvirtualBase.method() invoked!");
		methodInvoked = true;
	}
}
