package com.xamarin.interop;

public class CallNonvirtualBase {

	boolean methodInvoked;
	public void method () {
		System.out.println ("CallNonvirtualBase.method() invoked!");
		methodInvoked = true;
	}
}
