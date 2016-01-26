package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class CallVirtualFromConstructorBase implements GCUserPeerable {

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallVirtualFromConstructorBase (int value) {
		com.xamarin.java_interop.ManagedPeer.runConstructor (
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

	public void jiAddManagedReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void jiClearManagedReferences ()
	{
		managedReferences.clear ();
	}
}

