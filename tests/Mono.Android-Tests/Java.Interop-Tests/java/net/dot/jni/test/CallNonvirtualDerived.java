package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.android.IGCUserPeer;

public class CallNonvirtualDerived
		extends CallNonvirtualBase
		implements IGCUserPeer
{
	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallNonvirtualDerived () {
	}

	boolean methodInvoked;
	public void method () {
		System.out.println ("CallNonvirtualDerived.method() invoked!");
		methodInvoked = true;
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
