package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class RenameClassDerived
	extends RenameClassBase2
	implements GCUserPeerable
{
	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public RenameClassDerived () {
		System.out.println("RenameClassDerived.<init>()");
	}

	public int hashCode () {
		System.out.println("RenameClassDerived.hashCode()");
		return 64;
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
