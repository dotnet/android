package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class SelfRegistration implements GCUserPeerable {

	static  final   String  assemblyQualifiedName   = "Java.InteropTests.SelfRegistration, Java.Interop-Tests";

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public SelfRegistration () {
		if (SelfRegistration.class == getClass ()) {
			net.dot.jni.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					""
			);
		}
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
