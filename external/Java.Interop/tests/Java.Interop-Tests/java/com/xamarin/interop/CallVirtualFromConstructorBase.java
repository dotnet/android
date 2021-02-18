package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class CallVirtualFromConstructorBase implements GCUserPeerable {

	static  final   String  assemblyQualifiedName   = "Java.InteropTests.CallVirtualFromConstructorBase, Java.Interop-Tests";

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallVirtualFromConstructorBase (int value) {
		if (CallVirtualFromConstructorBase.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					"System.Int32",
					value
			);
		}
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

