package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

public class GetThis implements GCUserPeerable {

	static {
		net.dot.jni.ManagedPeer.registerNativeMembers (
				GetThis.class,
				"");
	}

	ArrayList<Object>       managedReferences     = new ArrayList<Object>();

	public GetThis () {
		if (GetThis.class == getClass ()) {
			net.dot.jni.ManagedPeer.construct (
					this,
					"()V"
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
