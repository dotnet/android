package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class RenameClassBase1
	implements GCUserPeerable
{
	static  final   String  assemblyQualifiedName   = "Java.InteropTests.RenameClassBase, Java.Interop-Tests";

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public RenameClassBase1 () {
		System.out.println("RenameClassBase.<init>()");
		if (RenameClassBase1.class == getClass ()) {
			net.dot.jni.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					""
			);
		}
	}

	public int hashCode () {
		System.out.println("RenameClassBase1.hashCode()");
		return 16;
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
