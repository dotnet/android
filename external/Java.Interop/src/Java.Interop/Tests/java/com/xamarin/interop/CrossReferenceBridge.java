package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class CrossReferenceBridge implements GCUserPeerable {

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public CrossReferenceBridge () {
		com.xamarin.java_interop.ManagedPeer.runConstructor (
				CrossReferenceBridge.class,
				this,
				"Java.InteropTests.CrossReferenceBridge, Java.Interop-Tests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null",
				""
		);
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
