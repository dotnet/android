package com.xamarin.interop;

public class CallNonvirtualDerived extends CallNonvirtualBase {

	public CallNonvirtualDerived () {
		com.xamarin.android.ManagedPeer.runConstructor (
				CallNonvirtualDerived.class,
				this,
				"Java.InteropTests.CallNonvirtualDerived, Java.Interop-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
				""
		);
	}

	boolean methodInvoked;
	public void method () {
		System.out.println ("CallNonvirtualDerived.method() invoked!");
		methodInvoked = true;
	}
}
