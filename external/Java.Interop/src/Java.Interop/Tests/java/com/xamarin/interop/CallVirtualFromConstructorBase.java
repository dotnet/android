package com.xamarin.interop;

public class CallVirtualFromConstructorBase {

	public CallVirtualFromConstructorBase (int value) {
		calledFromConstructor (value);
	}

	public void calledFromConstructor (int value) {
	}
}

