package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class CallVirtualFromConstructorBase implements GCUserPeerable {

	static {
		registerNatives ();
	}

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CallVirtualFromConstructorBase (int value) {
		if (CallVirtualFromConstructorBase.class == getClass ()) {
			nctor_0 (value);
		}
		calledFromConstructor (value);
	}

	private native void nctor_0 (int value);

	public void calledFromConstructor (int value) {
	}

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
			registerNatives.invoke (null, CallVirtualFromConstructorBase.class);
		} catch (Exception e) {
			throw new Error (e);
		}
	}
}
