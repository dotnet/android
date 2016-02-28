package com.xamarin.java_interop.internal;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

/* package */ final class JavaProxyObject
		extends java.lang.Object
		implements GCUserPeerable
{
	static  final   String  assemblyQualifiedName   = "Java.Interop.JavaProxyObject, Java.Interop, Version=0.1.0.0, Culture=neutral, PublicKeyToken=null";
	static {
		com.xamarin.java_interop.ManagedPeer.registerNativeMembers (
				JavaProxyObject.class,
				assemblyQualifiedName,
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

