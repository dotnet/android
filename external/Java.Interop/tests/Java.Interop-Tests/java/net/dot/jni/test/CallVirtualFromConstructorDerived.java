package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class CallVirtualFromConstructorDerived
		extends CallVirtualFromConstructorBase
		implements GCUserPeerable
{
	static {
		net.dot.jni.ManagedPeer.registerNativeMembers (
				CallVirtualFromConstructorDerived.class,
				"");
	}

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallVirtualFromConstructorDerived (int value) {
		super (value);
		if (CallVirtualFromConstructorDerived.class == getClass ()) {
			net.dot.jni.ManagedPeer.construct (
					this,
					"(I)V",
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

