package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

// Android constructs the managed peer through its runtime. GCUserPeerable
// keeps managed references alive for JavaObjectTest.DisposeAccessesThis.
public class GetThis implements GCUserPeerable {

	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public GetThis () {
	}

	public final GetThis getThis () {
		return this;
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
