package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class CallNonvirtualDerived2
		extends CallNonvirtualDerived
		implements GCUserPeerable
{
	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public CallNonvirtualDerived2 () {
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
