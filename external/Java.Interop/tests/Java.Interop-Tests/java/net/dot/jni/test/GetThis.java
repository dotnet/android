package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class GetThis implements GCUserPeerable {

	static  final   String  assemblyQualifiedName   = "Java.InteropTests.GetThis, Java.Interop-Tests";
	static {
		net.dot.jni.ManagedPeer.registerNativeMembers (
				GetThis.class,
				assemblyQualifiedName,
				"");
	}

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public GetThis () {
		if (GetThis.class == getClass ()) {
			net.dot.jni.ManagedPeer.construct (
					this,
					assemblyQualifiedName,
					""
			);
		}
	}
    
	public final GetThis getThis() {
		return this;
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
