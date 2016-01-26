package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class CallNonvirtualDerived
		extends CallNonvirtualBase
		implements GCUserPeerable
{
	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallNonvirtualDerived () {
		com.xamarin.java_interop.ManagedPeer.runConstructor (
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

	public void jiAddManagedReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void jiClearManagedReferences ()
	{
		managedReferences.clear ();
	}
}
