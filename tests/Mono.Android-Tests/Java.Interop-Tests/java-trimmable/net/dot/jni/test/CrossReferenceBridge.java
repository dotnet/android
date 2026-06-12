package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

// Variant of CrossReferenceBridge used by the Mono.Android.NET-Tests trimmable typemap lane.
//
// The desktop-JVM variant (../../../java/net/dot/jni/test/CrossReferenceBridge.java)
// calls net.dot.jni.ManagedPeer.construct() from its constructor. That native
// method is only registered by the Java.Interop test JVM and throws
// UnsatisfiedLinkError on Android. The managed CrossReferenceBridge peer is
// constructed by the normal Android JavaObject path, so this fixture only needs
// to implement GCUserPeerable for GC bridge cross-reference tracking.
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
