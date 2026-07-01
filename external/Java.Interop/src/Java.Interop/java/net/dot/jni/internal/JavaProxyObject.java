package net.dot.jni.internal;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

/* package */ final class JavaProxyObject
		extends java.lang.Object
		implements GCUserPeerable
{
	static {
		net.dot.jni.ManagedPeer.registerNativeMembers (
				JavaProxyObject.class,
				"");
	}

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	@Override
	public native boolean equals(Object obj);

	@Override
	public native int hashCode();

	@Override
	public native String toString();

	public void jiAddManagedReference (java.lang.Object obj)
	{
		managedReferences.add (obj);
	}

	public void jiClearManagedReferences ()
	{
		managedReferences.clear ();
	}
}

