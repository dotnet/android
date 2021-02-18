package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class CallNonvirtualDerived2
		extends CallNonvirtualDerived
		implements GCUserPeerable
{
	static  final   String  assemblyQualifiedName   = "Java.InteropTests.CallNonvirtualDerived2, Java.Interop-Tests";

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallNonvirtualDerived2 () {
		if (CallNonvirtualDerived2.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					""
			);
		}
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
