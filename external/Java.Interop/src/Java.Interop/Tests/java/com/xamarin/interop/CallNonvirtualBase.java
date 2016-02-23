package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class CallNonvirtualBase implements GCUserPeerable {

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallNonvirtualBase () {
		if (CallNonvirtualBase.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					"Java.InteropTests.CallNonvirtualBase, Java.Interop-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
					""
			);
		}
	}

	boolean methodInvoked;
	public void method () {
		System.out.println ("CallNonvirtualBase.method() invoked!");
		methodInvoked = true;
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
