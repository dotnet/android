package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.android.IGCUserPeer;

// Android constructs the managed peer through its runtime. IGCUserPeer
// keeps managed references alive for JavaObjectTest.DisposeAccessesThis.
public class GetThis implements IGCUserPeer {

	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public GetThis () {
	}

	public final GetThis getThis () {
		return this;
	}

	public void dotnetAddReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void dotnetClearReferences ()
	{
		managedReferences.clear ();
	}
}
