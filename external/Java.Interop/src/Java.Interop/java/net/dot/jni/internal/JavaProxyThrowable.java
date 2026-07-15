package net.dot.jni.internal;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

/* package */ final class JavaProxyThrowable
		extends java.lang.Error
		implements GCUserPeerable
{
	static {
		net.dot.jni.ManagedPeer.registerNativeMembers (
				JavaProxyThrowable.class,
				"");
	}

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public JavaProxyThrowable () {
	}

	public JavaProxyThrowable (String message) {
		super (message);
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

