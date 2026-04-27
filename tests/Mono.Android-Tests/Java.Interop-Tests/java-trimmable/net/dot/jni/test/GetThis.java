package net.dot.jni.test;

import java.util.ArrayList;

import net.dot.jni.GCUserPeerable;

// Variant of GetThis used by the Mono.Android.NET-Tests trimmable typemap lane.
//
// The desktop-JVM variant (../../../java/net/dot/jni/test/GetThis.java) relies
// on net.dot.jni.ManagedPeer.registerNativeMembers / ManagedPeer.construct;
// those native methods are only registered by the Java.Interop test JVM and
// throw UnsatisfiedLinkError on Android, which makes the static initializer
// here fail when the class is loaded on a real device.
//
// Under the Android trimmable typemap, the C# `GetThis : JavaObject` peer is
// constructed via the standard Java.Interop / Mono.Android peer construction
// path, so the Java class only needs:
//   - getThis() — exercised by JavaObjectTest.DisposeAccessesThis
//   - GCUserPeerable implementation so the runtime registers managed-side
//     references (see Android.Runtime.JNIEnv.IsGCUserPeer)
public class GetThis implements GCUserPeerable {

	ArrayList<Object> managedReferences = new ArrayList<Object>();

	public GetThis () {
	}

	public final GetThis getThis () {
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
