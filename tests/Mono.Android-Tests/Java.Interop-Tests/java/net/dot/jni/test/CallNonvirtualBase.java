package net.dot.jni.test;

import java.util.ArrayList;

import mono.android.IGCUserPeer;

public class CallNonvirtualBase implements IGCUserPeer {

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallNonvirtualBase () {
	}

	boolean methodInvoked;
	public void method () {
		System.out.println ("CallNonvirtualBase.method() invoked!");
		methodInvoked = true;
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
