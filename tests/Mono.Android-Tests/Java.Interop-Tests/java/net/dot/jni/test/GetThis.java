package net.dot.jni.test;

import java.util.ArrayList;

import mono.android.IGCUserPeer;

// Android constructs the managed peer through its runtime. IGCUserPeer
// keeps managed references alive for JavaObjectTest.DisposeAccessesThis.
public class GetThis implements IGCUserPeer {

	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public GetThis () {
	}

	public final GetThis getThis () {
		return this;
	}

	public void monodroidAddReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void monodroidClearReferences ()
	{
		managedReferences.clear ();
	}
}
