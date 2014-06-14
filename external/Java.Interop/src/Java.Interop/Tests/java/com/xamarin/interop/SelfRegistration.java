package com.xamarin.interop;

public class SelfRegistration {

	public SelfRegistration () {
		com.xamarin.android.ManagedPeer.runConstructor (
				SelfRegistration.class,
				this,
				"Java.InteropTests.SelfRegistration, Java.Interop-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
				""
		);
	}
}
