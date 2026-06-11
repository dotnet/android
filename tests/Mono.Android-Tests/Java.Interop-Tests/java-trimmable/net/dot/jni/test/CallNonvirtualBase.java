package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class CallNonvirtualBase implements GCUserPeerable {

	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public CallNonvirtualBase () {
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
