package com.xamarin.interop;

public class CallVirtualFromConstructorDerived extends CallVirtualFromConstructorBase {

	public CallVirtualFromConstructorDerived (int value) {
		super (value);
	}

	public native void calledFromConstructor (int value);
}

