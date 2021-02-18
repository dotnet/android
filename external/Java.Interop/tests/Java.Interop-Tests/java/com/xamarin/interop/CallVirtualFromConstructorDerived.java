package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class CallVirtualFromConstructorDerived
		extends CallVirtualFromConstructorBase
		implements GCUserPeerable
{
	static  final   String  assemblyQualifiedName   = "Java.InteropTests.CallVirtualFromConstructorDerived, Java.Interop-Tests";
	static {
		com.xamarin.java_interop.ManagedPeer.registerNativeMembers (
				CallVirtualFromConstructorDerived.class,
				assemblyQualifiedName,
				"");
	}

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallVirtualFromConstructorDerived (int value) {
		super (value);
		if (CallVirtualFromConstructorDerived.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
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

