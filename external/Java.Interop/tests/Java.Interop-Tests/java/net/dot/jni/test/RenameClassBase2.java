package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class RenameClassBase2
	extends RenameClassBase1
	implements GCUserPeerable
{
	static  final   String  assemblyQualifiedName   = "Java.InteropTests.RenameClassBase, Java.Interop-Tests";

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public RenameClassBase2 () {
		System.out.println("RenameClassBase.<init>()");
		if (RenameClassBase2.class == getClass ()) {
			net.dot.jni.ManagedPeer.construct (
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
