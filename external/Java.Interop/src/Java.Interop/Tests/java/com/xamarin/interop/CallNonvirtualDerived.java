package com.xamarin.interop;

public class CallNonvirtualDerived extends CallNonvirtualBase {

	boolean methodInvoked;
	public void method () {
		System.out.println ("CallNonvirtualDerived.method() invoked!");
		methodInvoked = true;
	}
}
