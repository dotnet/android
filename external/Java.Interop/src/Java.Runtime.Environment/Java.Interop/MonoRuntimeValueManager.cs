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

		GetThreadDescriptionCb                      getThreadDescription;

		public override void OnSetRuntime (JniRuntime runtime)
		{
			base.OnSetRuntime (runtime);
			getThreadDescription    = x => java_interop_strdup (runtime.GetCurrentThreadDescription ());

			var bridge = java_interop_gc_bridge_get_current ();
			if (bridge != IntPtr.Zero)
				return;

			bridge  = java_interop_gc_bridge_new (runtime.InvocationPointer);
			if (bridge == IntPtr.Zero)
				throw new NotSupportedException ("Could not initialize JNI::Mono GC Bridge!");

			try {
				if (java_interop_gc_bridge_set_thread_description_creator (bridge, getThreadDescription, IntPtr.Zero) < 0)
					throw new NotSupportedException ("Could not set thread description creator!");
				if (java_interop_gc_bridge_set_bridge_processing_field (bridge, typeof (MonoRuntimeValueManager).TypeHandle, nameof (GCBridgeProcessingIsActive)) < 0)
					throw new NotSupportedException ("Could not set bridge processing field!");
				foreach (var t in new[]{typeof (JavaObject), typeof (JavaException)}) {
					if (java_interop_gc_bridge_register_bridgeable_type (bridge, t.TypeHandle) < 0)
						throw new NotSupportedException ("Could not register type " + t.FullName + "!");
				}
				if (java_interop_gc_bridge_set_current_once (bridge) < 0)
					throw new NotSupportedException ("Could not set GC Bridge instance!");
			}
			catch (Exception) {
				java_interop_gc_bridge_free (bridge);
				throw;
			}
			if (java_interop_gc_bridge_register_hooks_once (GCBridgeUseWeakReferenceKind.Jni) < 0)
				throw new NotSupportedException ("Could not register GC Bridge with Mono!");
		}

		public override void WaitForGCBridgeProcessing ()
		{
			if (!GCBridgeProcessingIsActive)
				return;
			java_interop_gc_bridge_wait_for_bridge_processing ();
		}

		const   string JavaInteropLib = "java-interop";

		delegate    IntPtr  GetThreadDescriptionCb (IntPtr user_data);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern IntPtr java_interop_gc_bridge_get_current ();

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern int java_interop_gc_bridge_set_current_once (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern int java_interop_gc_bridge_register_hooks_once (GCBridgeUseWeakReferenceKind weak_ref_kind);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern IntPtr java_interop_gc_bridge_new (IntPtr jvm);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern int java_interop_gc_bridge_free (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern int java_interop_gc_bridge_set_thread_description_creator (IntPtr bridge, GetThreadDescriptionCb creator, IntPtr user_data);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern IntPtr java_interop_strdup (string value);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern int java_interop_gc_bridge_set_bridge_processing_field (IntPtr bridge, RuntimeTypeHandle type_handle, string field_name);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern int java_interop_gc_bridge_register_bridgeable_type (IntPtr bridge, RuntimeTypeHandle type_handle);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		static extern void java_interop_gc_bridge_wait_for_bridge_processing ();
	}
}

