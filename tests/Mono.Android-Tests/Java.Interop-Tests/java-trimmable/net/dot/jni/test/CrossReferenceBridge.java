package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

// Android trimmable typemap variant of the Java.Interop CrossReferenceBridge
// fixture. The desktop JVM fixture calls net.dot.jni.ManagedPeer.construct()
// from its constructor; the trimmable Android runtime intentionally does not
// ship ManagedPeer, and managed peer construction is handled by generated
// typemap proxies instead.
public class CrossReferenceBridge implements GCUserPeerable {

	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public CrossReferenceBridge () {
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
