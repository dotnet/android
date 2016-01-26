package com.xamarin.java_interop.internal;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

/* package */ final class JavaProxyThrowable
		extends java.lang.Throwable
		implements GCUserPeerable
{
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

