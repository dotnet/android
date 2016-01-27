using System;
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

