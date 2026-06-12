package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class CallNonvirtualDerived
		extends CallNonvirtualBase
		implements GCUserPeerable
{
	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public CallNonvirtualDerived () {
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
