package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class CallVirtualFromConstructorDerived
		extends CallVirtualFromConstructorBase
		implements GCUserPeerable
{
	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallVirtualFromConstructorDerived (int value) {
		super (value);
		if (CallVirtualFromConstructorDerived.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					"Java.InteropTests.CallVirtualFromConstructorDerived, Java.Interop-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
					"System.Int32",
					value
			);
		}
	}

	public static CallVirtualFromConstructorDerived newInstance (int value)
	{
		return new CallVirtualFromConstructorDerived (value);
	}

	public native void calledFromConstructor (int value);

	public void jiAddManagedReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void jiClearManagedReferences ()
	{
		managedReferences.clear ();
	}
}

