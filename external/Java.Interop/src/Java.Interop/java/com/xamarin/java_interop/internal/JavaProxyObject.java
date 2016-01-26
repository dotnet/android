package com.xamarin.java_interop.internal;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

/* package */ final class JavaProxyObject
		extends java.lang.Object
		implements GCUserPeerable
{
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

