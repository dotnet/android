using System;
using System.Collections.Generic;
using System.Linq;
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

		public override void Collect ()
		{
			GC.Collect ();
		}

		protected override void Dispose (bool disposing)
		{
			if (!disposing)
				return;

			if (RegisteredInstances == null)
				return;

			foreach (var o in RegisteredInstances.Values) {
				IJavaPeerable t;
				if (!o.TryGetTarget (out t))
					continue;
				t.Dispose ();
			}
			RegisteredInstances.Clear ();
		}

		Dictionary<int, WeakReference<IJavaPeerable>>  RegisteredInstances = new Dictionary<int, WeakReference<IJavaPeerable>>();


		public override List<WeakReference<IJavaPeerable>> GetSurfacedObjects ()
		{
			lock (RegisteredInstances) {
				return RegisteredInstances.Values.ToList ();
			}
		}

		public override void Add (IJavaPeerable value)
		{
			var r = value.PeerReference;
			if (!r.IsValid)
				throw new ObjectDisposedException (value.GetType ().FullName);
			var o = PeekObject (value.PeerReference);
			if (o != null)
				return;

			if (r.Type != JniObjectReferenceType.Global) {
				value.SetPeerReference (r.NewGlobalRef ());
				JniObjectReference.Dispose (ref r, JniObjectReferenceOptions.CopyAndDispose);
			}
			int key = value.JniIdentityHashCode;
			lock (RegisteredInstances) {
				WeakReference<IJavaPeerable>    existing;
				IJavaPeerable     target;
				if (RegisteredInstances.TryGetValue (key, out existing) && existing.TryGetTarget (out target))
					Runtime.ObjectReferenceManager.WriteGlobalReferenceLine (
							"Warning: Not registering PeerReference={0} IdentityHashCode=0x{1} Instance={2} Instance.Type={3} Java.Type={4}; " +
							"keeping previously registered PeerReference={5} Instance={6} Instance.Type={7} Java.Type={8}.",
							value.PeerReference.ToString (),
							key.ToString ("x"),
							RuntimeHelpers.GetHashCode (value).ToString ("x"),
							value.GetType ().FullName,
							JniEnvironment.Types.GetJniTypeNameFromInstance (value.PeerReference),
							target.PeerReference.ToString (),
							RuntimeHelpers.GetHashCode (target).ToString ("x"),
							target.GetType ().FullName,
							JniEnvironment.Types.GetJniTypeNameFromInstance (target.PeerReference));
				else
					RegisteredInstances [key] = new WeakReference<IJavaPeerable> (value, trackResurrection: true);
			}
		}

		public override void Remove (IJavaPeerable value)
		{
			int key = value.JniIdentityHashCode;
			lock (RegisteredInstances) {
				WeakReference<IJavaPeerable>  wv;
				IJavaPeerable                 t;
				if (RegisteredInstances.TryGetValue (key, out wv) &&
						wv.TryGetTarget (out t) &&
						object.ReferenceEquals (value, t))
					RegisteredInstances.Remove (key);
			}
		}

		public override IJavaPeerable PeekObject (JniObjectReference reference)
		{
			if (!reference.IsValid)
				return null;

			int key = GetJniIdentityHashCode (reference);

			lock (RegisteredInstances) {
				WeakReference<IJavaPeerable>    wv;
				if (RegisteredInstances.TryGetValue (key, out wv)) {
					IJavaPeerable target;
					if (wv.TryGetTarget (out target))
						return target;
				}
				RegisteredInstances.Remove (key);
			}
			return null;
		}

		public override void Finalize (IJavaPeerable value)
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
				Remove (value);
				value.SetPeerReference (new JniObjectReference ());
				value.Finalized ();
				return;
			}

			try {
				bool collected  = TryGC (value, ref h);
				if (collected) {
					Remove (value);
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

