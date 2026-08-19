package net.dot.jni.internal;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

/* package */ final class JavaProxyObject
		extends java.lang.Object
		implements GCUserPeerable
{
	ArrayList<Object>       managedReferences     = new ArrayList<Object> ();

	// Trimmable proxies use Java identity semantics instead of Java.Interop's
	// native equals/hashCode/toString callbacks.
	@Override
	public boolean equals (Object obj)
	{
		return this == obj;
	}

	@Override
	public int hashCode ()
	{
		return System.identityHashCode (this);
	}

	@Override
	public String toString ()
	{
		return super.toString ();
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
