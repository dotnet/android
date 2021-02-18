package com.xamarin.java_interop.internal;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

/* package */ final class JavaProxyThrowable
		extends java.lang.Error
		implements GCUserPeerable
{
	static  final   String  assemblyQualifiedName   = "Java.Interop.JavaProxyThrowable, Java.Interop";
	static {
		com.xamarin.java_interop.ManagedPeer.registerNativeMembers (
				JavaProxyThrowable.class,
				assemblyQualifiedName,
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

