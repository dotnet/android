package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class CallVirtualFromConstructorDerived
		extends CallVirtualFromConstructorBase
		implements GCUserPeerable
{
	static {
		registerNatives ();
	}

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();
	boolean                 calledFromConstructorInvoked;

	public CallVirtualFromConstructorDerived (int value) {
		super (value);
		if (CallVirtualFromConstructorDerived.class == getClass () && !calledFromConstructorInvoked) {
			nctor_0 (value);
		}
	}

	public static CallVirtualFromConstructorDerived newInstance (int value)
	{
		return new CallVirtualFromConstructorDerived (value);
	}

	public void calledFromConstructor (int value) {
		calledFromConstructorInvoked = true;
		n_CalledFromConstructor (value);
	}

	public native void n_CalledFromConstructor (int value);

	private native void nctor_0 (int value);

	public void jiAddManagedReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void jiClearManagedReferences ()
	{
		managedReferences.clear ();
	}

	static void registerNatives ()
	{
		try {
			Class<?> runtime = Class.forName ("mono.android.Runtime");
			java.lang.reflect.Method registerNatives = runtime.getMethod ("registerNatives", Class.class);
			registerNatives.invoke (null, CallVirtualFromConstructorDerived.class);
		} catch (Exception e) {
			throw new Error (e);
		}
	}
}
