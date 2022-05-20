package com.xamarin.interop;

import java.util.ArrayList;

import com.xamarin.java_interop.GCUserPeerable;

public class RenameClassBase2
	extends RenameClassBase1
	implements GCUserPeerable
{
	static  final   String  assemblyQualifiedName   = "Java.InteropTests.RenameClassBase, Java.Interop-Tests";

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public RenameClassBase2 () {
		System.out.println("RenameClassBase.<init>()");
		if (RenameClassBase2.class == getClass ()) {
			com.xamarin.java_interop.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					""
			);
		}
	}

	public int hashCode () {
		System.out.println("RenameClassBase2.hashCode()");
		return 32;
	}

	public int myNewHashCode() {
		System.out.println("RenameClassBase2.myNewHashCode()");
		return 33;
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
