package com.xamarin.interop;

public class CallNonvirtualBase {

	public CallNonvirtualBase () {
		com.xamarin.android.ManagedPeer.runConstructor (
				CallNonvirtualBase.class,
				this,
				"Java.InteropTests.CallNonvirtualBase, Java.Interop-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
				""
		);
	}

	boolean methodInvoked;
	public void method () {
		System.out.println ("CallNonvirtualBase.method() invoked!");
		methodInvoked = true;
	}
}
