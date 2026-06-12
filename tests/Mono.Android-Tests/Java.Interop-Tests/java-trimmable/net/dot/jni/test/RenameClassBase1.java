package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class RenameClassBase1
	implements GCUserPeerable
{
	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public RenameClassBase1 () {
		System.out.println("RenameClassBase.<init>()");
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
