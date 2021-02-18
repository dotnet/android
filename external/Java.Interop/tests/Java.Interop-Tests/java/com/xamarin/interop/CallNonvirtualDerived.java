package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class CallNonvirtualDerived
		extends CallNonvirtualBase
		implements GCUserPeerable
{
	static  final   String  assemblyQualifiedName   = "Java.InteropTests.CallNonvirtualDerived, Java.Interop-Tests";

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallNonvirtualDerived () {
		if (CallNonvirtualDerived.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					""
			);
		}
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
