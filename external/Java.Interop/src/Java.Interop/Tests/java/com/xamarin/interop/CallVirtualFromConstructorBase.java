package com.xamarin.interop;

public class CallVirtualFromConstructorBase {

	public CallVirtualFromConstructorBase (int value) {
		com.xamarin.android.ManagedPeer.runConstructor (
				CallVirtualFromConstructorBase.class,
				this,
				"Java.InteropTests.CallVirtualFromConstructorBase, Java.Interop-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
				"System.Int32",
				value
		);
		calledFromConstructor (value);
	}

	public void calledFromConstructor (int value) {
	}
}

