using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Android.Runtime;

namespace Java.Interop {

	public static class Runtime {

		[Obsolete ("Please use Java.Interop.JniEnvironment.Runtime.ValueManager.GetSurfacedPeers()")]
		public static List<WeakReference> GetSurfacedObjects ()
		{
			var peers = JNIEnvInit.ValueManager!.GetSurfacedPeers ();
			var r = new List<WeakReference> (peers.Count);
			foreach (var p in peers) {
				if (p.SurfacedPeer.TryGetTarget (out var target))
					r.Add (new WeakReference (target, trackResurrection: true));
			}
			return r;
		}

		public static int MaxGlobalReferenceCount {
			get {return RuntimeNativeMethods._monodroid_max_gref_get ();}
		}

		public static int GlobalReferenceCount {
			get {return RuntimeNativeMethods._monodroid_gref_get ();}
		}

		public static int LocalReferenceCount {
#if JAVA_INTEROP
			get {return JniEnvironment.LocalReferenceCount;}
#else   // !JAVA_INTEROP
			get {return JNIEnv.lref_count;}
#endif  // !JAVA_INTEROP
		}

		public static bool IsGCUserPeer (IJavaObject value)
		{
			if (value == null)
				return false;
			return IsGCUserPeer (value.Handle);
		}

		public static bool IsGCUserPeer (IntPtr value)
		{
			return JNIEnv.IsGCUserPeer (value);
		}
	}
}
