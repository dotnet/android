package com.xamarin.interop;

public class CallVirtualFromConstructorDerived extends CallVirtualFromConstructorBase {

	public CallVirtualFromConstructorDerived (int value) {
		super (value);
		com.xamarin.android.ManagedPeer.runConstructor (
				CallVirtualFromConstructorDerived.class,
				this,
				"Java.InteropTests.CallVirtualFromConstructorDerived, Java.Interop-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
				"System.Int32",
				value
		);
	}

	public native void calledFromConstructor (int value);
}

