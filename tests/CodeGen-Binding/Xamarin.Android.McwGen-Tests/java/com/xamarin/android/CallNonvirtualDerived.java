package com.xamarin.android;

public class CallNonvirtualDerived extends CallNonvirtualBase {

	public boolean methodInvoked;
	public void method () {
		System.out.println ("CallNonvirtualDerived.method() invoked!");
		methodInvoked = true;
	}
}
