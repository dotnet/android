using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using Android.Runtime;

namespace Java.Interop {

	public static class Runtime {

		[Obsolete ("Please use Java.Interop.JniEnvironment.Runtime.ValueManager.GetSurfacedPeers()")]
		public static List<WeakReference> GetSurfacedObjects ()
		{
			var peers = JniEnvironment.Runtime.ValueManager.GetSurfacedPeers ();
			var r = new List<WeakReference> (peers.Count);
			foreach (var p in peers) {
				if (p.SurfacedPeer.TryGetTarget (out var target))
					r.Add (new WeakReference (target, trackResurrection: true));
			}
			return r;
		}

		public static int MaxGlobalReferenceCount {
			get {return JNIEnvInit.max_gref_count;}
		}

		public static int GlobalReferenceCount {
			get {return JniEnvironment.Runtime.GlobalReferenceCount;}
		}

		public static int LocalReferenceCount {
			get {return JniEnvironment.LocalReferenceCount;}
		}

		public static bool IsGCUserPeer (IJavaObject value)
		{
			if (value == null)
				return false;
			unsafe {
				// SAFETY: value owns the borrowed JNI peer; KeepAlive retains that owner past the synchronous type test.
				bool result = IsGCUserPeer (value.Handle);
				GC.KeepAlive (value);
				return result;
			}
		}

		/// <safety>
		/// A nonzero <paramref name="value"/> must be a live JNI object reference from this VM usable
		/// on this thread until the type test returns or throws. Exclude concurrent release; zero returns false.
		/// </safety>
		public static unsafe bool IsGCUserPeer (IntPtr value)
		{
			return JNIEnv.IsGCUserPeer (value);
		}
	}
}
