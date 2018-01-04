using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Java.Interop {

	enum GCBridgeUseWeakReferenceKind {
		Java,
		Jni,
	}

	class MonoRuntimeValueManager : JniRuntime.JniValueManager {

		#pragma warning disable 0649
		// This field is mutated by the java-interop native lib
		static  volatile    bool                    GCBridgeProcessingIsActive;
		#pragma warning restore 0649

		IntPtr                                      bridge;

		public override void OnSetRuntime (JniRuntime runtime)
		{
			base.OnSetRuntime (runtime);

			bridge  = NativeMethods.java_interop_gc_bridge_get_current ();
			if (bridge != IntPtr.Zero)
				return;

			bridge  = NativeMethods.java_interop_gc_bridge_new (runtime.InvocationPointer);
			if (bridge == IntPtr.Zero)
				throw new NotSupportedException ("Could not initialize JNI::Mono GC Bridge!");

			try {
				if (NativeMethods.java_interop_gc_bridge_set_bridge_processing_field (bridge, typeof (MonoRuntimeValueManager).TypeHandle, nameof (GCBridgeProcessingIsActive)) < 0)
					throw new NotSupportedException ("Could not set bridge processing field!");
				foreach (var t in new[]{typeof (JavaObject), typeof (JavaException)}) {
					if (NativeMethods.java_interop_gc_bridge_register_bridgeable_type (bridge, t.TypeHandle) < 0)
						throw new NotSupportedException ("Could not register type " + t.FullName + "!");
				}
				if (NativeMethods.java_interop_gc_bridge_add_current_app_domain (bridge) < 0)
					throw new NotSupportedException ("Could not register current AppDomain!");
				if (NativeMethods.java_interop_gc_bridge_set_current_once (bridge) < 0)
					throw new NotSupportedException ("Could not set GC Bridge instance!");
			}
			catch (Exception) {
				NativeMethods.java_interop_gc_bridge_free (bridge);
				bridge  = IntPtr.Zero;
				throw;
			}
			if (NativeMethods.java_interop_gc_bridge_register_hooks (bridge, GCBridgeUseWeakReferenceKind.Jni) < 0)
				throw new NotSupportedException ("Could not register GC Bridge with Mono!");
		}

		public override void WaitForGCBridgeProcessing ()
		{
			if (!GCBridgeProcessingIsActive)
				return;
			NativeMethods.java_interop_gc_bridge_wait_for_bridge_processing (bridge);
		}

		public override void CollectPeers ()
		{
			GC.Collect ();
		}

		protected override void Dispose (bool disposing)
		{
			base.Dispose (disposing);

			if (!disposing)
				return;

			if (RegisteredInstances == null)
				return;

			lock (RegisteredInstances) {
				foreach (var o in RegisteredInstances.Values) {
					foreach (var r in o) {
						IJavaPeerable t;
						if (!r.TryGetTarget (out t))
							continue;
						t.Dispose ();
					}
				}
				RegisteredInstances.Clear ();
				RegisteredInstances = null;
			}
		}

		Dictionary<int, List<WeakReference<IJavaPeerable>>>     RegisteredInstances = new Dictionary<int, List<WeakReference<IJavaPeerable>>>();


		public override List<JniSurfacedPeerInfo> GetSurfacedPeers ()
		{
			lock (RegisteredInstances) {
				var peers = new List<JniSurfacedPeerInfo> (RegisteredInstances.Count);
				foreach (var e in RegisteredInstances) {
					foreach (var p in e.Value) {
						peers.Add (new JniSurfacedPeerInfo (e.Key, p));
					}
				}
				return peers;
			}
		}

		public override void AddPeer (IJavaPeerable value)
		{
			var r = value.PeerReference;
			if (!r.IsValid)
				throw new ObjectDisposedException (value.GetType ().FullName);
			var o = PeekPeer (value.PeerReference);
			if (o != null)
				return;

			if (r.Type != JniObjectReferenceType.Global) {
				value.SetPeerReference (r.NewGlobalRef ());
				JniObjectReference.Dispose (ref r, JniObjectReferenceOptions.CopyAndDispose);
			}
			int key = value.JniIdentityHashCode;
			lock (RegisteredInstances) {
				List<WeakReference<IJavaPeerable>> peers;
				if (!RegisteredInstances.TryGetValue (key, out peers)) {
					peers = new List<WeakReference<IJavaPeerable>> () {
						new WeakReference<IJavaPeerable>(value, trackResurrection: true),
					};
					RegisteredInstances.Add (key, peers);
					return;
				}

				for (int i = peers.Count - 1; i >= 0; i--) {
					var wp = peers [i];
					IJavaPeerable   p;
					if (!wp.TryGetTarget (out p)) {
						// Peer was collected
						peers.RemoveAt (i);
						continue;
					}
					if (!JniEnvironment.Types.IsSameObject (p.PeerReference, value.PeerReference))
						continue;
					if (Replaceable (p)) {
						peers [i] = new WeakReference<IJavaPeerable>(value, trackResurrection: true);
					} else {
						WarnNotReplacing (key, value, p);
					}
					return;
				}
				peers.Add (new WeakReference<IJavaPeerable> (value, trackResurrection: true));
			}
		}

		void WarnNotReplacing (int key, IJavaPeerable ignoreValue, IJavaPeerable keepValue)
		{
			Runtime.ObjectReferenceManager.WriteGlobalReferenceLine (
					"Warning: Not registering PeerReference={0} IdentityHashCode=0x{1} Instance={2} Instance.Type={3} Java.Type={4}; " +
					"keeping previously registered PeerReference={5} Instance={6} Instance.Type={7} Java.Type={8}.",
					ignoreValue.PeerReference.ToString (),
					key.ToString ("x"),
					RuntimeHelpers.GetHashCode (ignoreValue).ToString ("x"),
					ignoreValue.GetType ().FullName,
					JniEnvironment.Types.GetJniTypeNameFromInstance (ignoreValue.PeerReference),
					keepValue.PeerReference.ToString (),
					RuntimeHelpers.GetHashCode (keepValue).ToString ("x"),
					keepValue.GetType ().FullName,
					JniEnvironment.Types.GetJniTypeNameFromInstance (keepValue.PeerReference));
		}

		static bool Replaceable (IJavaPeerable peer)
		{
			if (peer == null)
				return true;
			return (peer.JniManagedPeerState & JniManagedPeerStates.Replaceable) == JniManagedPeerStates.Replaceable;
		}

		public override void RemovePeer (IJavaPeerable value)
		{
			if (value == null)
				throw new ArgumentNullException (nameof (value));

			int key = value.JniIdentityHashCode;
			lock (RegisteredInstances) {
				List<WeakReference<IJavaPeerable>> peers;
				if (!RegisteredInstances.TryGetValue (key, out peers))
					return;

				for (int i = peers.Count - 1; i >= 0; i--) {
					var wp = peers [i];
					IJavaPeerable   p;
					if (!wp.TryGetTarget (out p)) {
						// Peer was collected
						peers.RemoveAt (i);
						continue;
					}
					if (object.ReferenceEquals (value, p)) {
						peers.RemoveAt (i);
					}
				}
				if (peers.Count == 0)
					RegisteredInstances.Remove (key);
			}
		}

		public override IJavaPeerable PeekPeer (JniObjectReference reference)
		{
			if (!reference.IsValid)
				return null;

			int key = GetJniIdentityHashCode (reference);

			lock (RegisteredInstances) {
				List<WeakReference<IJavaPeerable>> peers;
				if (!RegisteredInstances.TryGetValue (key, out peers))
					return null;

				for (int i = peers.Count - 1; i >= 0; i--) {
					var wp = peers [i];
					IJavaPeerable   p;
					if (!wp.TryGetTarget (out p)) {
						// Peer was collected
						peers.RemoveAt (i);
						continue;
					}
					if (JniEnvironment.Types.IsSameObject (reference, p.PeerReference))
						return p;
				}
				if (peers.Count == 0)
					RegisteredInstances.Remove (key);
			}
			return null;
		}

		public override void FinalizePeer (IJavaPeerable value)
		{
			var h = value.PeerReference;
			var o = Runtime.ObjectReferenceManager;
			// MUST NOT use SafeHandle.ReferenceType: local refs are tied to a JniEnvironment
			// and the JniEnvironment's corresponding thread; it's a thread-local value.
			// Accessing SafeHandle.ReferenceType won't kill anything (so far...), but
			// instead it always returns JniReferenceType.Invalid.
			if (!h.IsValid || h.Type == JniObjectReferenceType.Local) {
				if (o.LogGlobalReferenceMessages) {
					o.WriteGlobalReferenceLine ("Finalizing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}",
							h.ToString (),
							value.JniIdentityHashCode.ToString ("x"),
							RuntimeHelpers.GetHashCode (value).ToString ("x"),
							value.GetType ().ToString ());
				}
				RemovePeer (value);
				value.SetPeerReference (new JniObjectReference ());
				value.Finalized ();
				return;
			}

			try {
				bool collected  = TryGC (value, ref h);
				if (collected) {
					RemovePeer (value);
					value.SetPeerReference (new JniObjectReference ());
					if (o.LogGlobalReferenceMessages) {
						o.WriteGlobalReferenceLine ("Finalizing PeerReference={0} IdentityHashCode=0x{1} Instance=0x{2} Instance.Type={3}",
								h.ToString (),
								value.JniIdentityHashCode.ToString ("x"),
								RuntimeHelpers.GetHashCode (value).ToString ("x"),
								value.GetType ().ToString ());
					}
					value.Finalized ();
				} else {
					value.SetPeerReference (h);
					GC.ReRegisterForFinalize (value);
				}
			} catch (Exception e) {
				Runtime.FailFast ("Unable to perform a GC! " + e);
			}
		}

		/// <summary>
		///   Try to garbage collect <paramref name="value"/>.
		/// </summary>
		/// <returns>
		///   <c>true</c>, if <paramref name="value"/> was collected and
		///   <paramref name="handle"/> is invalid; otherwise <c>false</c>.
		/// </returns>
		/// <param name="value">
		///   The <see cref="T:Java.Interop.IJavaPeerable"/> instance to collect.
		/// </param>
		/// <param name="handle">
		///   The <see cref="T:Java.Interop.JniObjectReference"/> of <paramref name="value"/>.
		///   This value may be updated, and <see cref="P:Java.Interop.IJavaObject.PeerReference"/>
		///   will be updated with this value.
		/// </param>
		internal protected virtual bool TryGC (IJavaPeerable value, ref JniObjectReference handle)
		{
			if (!handle.IsValid)
				return true;
			var wgref = handle.NewWeakGlobalRef ();
			JniObjectReference.Dispose (ref handle);
			JniGC.Collect ();
			handle = wgref.NewGlobalRef ();
			JniObjectReference.Dispose (ref wgref);
			return !handle.IsValid;
		}
	}

	static class JavaLangRuntime {
		static JniType _typeRef;
		static JniType TypeRef {
			get {return JniType.GetCachedJniType (ref _typeRef, "java/lang/Runtime");}
		}

		static JniMethodInfo _getRuntime;
		internal static JniObjectReference GetRuntime ()
		{
			TypeRef.GetCachedStaticMethod (ref _getRuntime, "getRuntime", "()Ljava/lang/Runtime;");
			return JniEnvironment.StaticMethods.CallStaticObjectMethod (TypeRef.PeerReference, _getRuntime);
		}

		static JniMethodInfo _gc;
		internal static void GC (JniObjectReference runtime)
		{
			TypeRef.GetCachedInstanceMethod (ref _gc, "gc", "()V");
			JniEnvironment.InstanceMethods.CallVoidMethod (runtime, _gc);
		}
	}

	static class JniGC {

		internal static void Collect ()
		{
			var runtime = JavaLangRuntime.GetRuntime ();
			try {
				JavaLangRuntime.GC (runtime);
			} finally {
				JniObjectReference.Dispose (ref runtime);
			}
		}
	}

	partial class NativeMethods {

		const   string JavaInteropLib = "java-interop";

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern IntPtr java_interop_gc_bridge_get_current ();

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_set_current_once (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_register_hooks (IntPtr bridge, GCBridgeUseWeakReferenceKind weak_ref_kind);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern IntPtr java_interop_gc_bridge_new (IntPtr jvm);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_free (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_add_current_app_domain (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_remove_current_app_domain (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern IntPtr java_interop_strdup (string value);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_set_bridge_processing_field (IntPtr bridge, RuntimeTypeHandle type_handle, string field_name);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_register_bridgeable_type (IntPtr bridge, RuntimeTypeHandle type_handle);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern void java_interop_gc_bridge_wait_for_bridge_processing (IntPtr bridge);
	}
}

