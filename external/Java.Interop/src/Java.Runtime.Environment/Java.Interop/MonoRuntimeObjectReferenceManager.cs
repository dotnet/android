using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Java.Interop {

	class MonoRuntimeObjectReferenceManager : JniRuntime.JniObjectReferenceManager {

		IntPtr                  bridge;
		bool                    logGlobalRefs;
		bool                    logLocalRefs;

		public override void OnSetRuntime (JniRuntime runtime)
		{
			base.OnSetRuntime (runtime);
			bridge  = JreNativeMethods.java_interop_gc_bridge_get_current ();
			if (bridge != IntPtr.Zero) {
				logLocalRefs    = JreNativeMethods.java_interop_gc_bridge_lref_get_log_file (bridge) != IntPtr.Zero;
				logGlobalRefs   = JreNativeMethods.java_interop_gc_bridge_gref_get_log_file (bridge) != IntPtr.Zero;
			}
		}

		public override int GlobalReferenceCount {
			get {return JreNativeMethods.java_interop_gc_bridge_get_gref_count (bridge);}
		}

		public override int WeakGlobalReferenceCount {
			get {return JreNativeMethods.java_interop_gc_bridge_get_weak_gref_count (bridge);}
		}

		public override bool LogLocalReferenceMessages {
			get {return logLocalRefs;}
		}

		public override void WriteLocalReferenceLine (string format, params object[] args)
		{
			if (!LogLocalReferenceMessages)
				return;
			JreNativeMethods.java_interop_gc_bridge_lref_log_message (bridge, 0, string.Format (format, args));
			JreNativeMethods.java_interop_gc_bridge_lref_log_message (bridge, 0, "\n");
		}

		public override JniObjectReference CreateLocalReference (JniObjectReference reference, ref int localReferenceCount)
		{
			if (!reference.IsValid)
				return reference;

			var r = base.CreateLocalReference (reference, ref localReferenceCount);
			JreNativeMethods.java_interop_gc_bridge_lref_log_new (bridge,
					localReferenceCount,
					reference.Handle,
					ToByte (reference.Type),
					r.Handle,
					ToByte (r.Type),
					GetCurrentManagedThreadName (LogLocalReferenceMessages),
					Environment.CurrentManagedThreadId,
					GetCurrentManagedThreadStack (LogLocalReferenceMessages));
			return r;
		}

		string? GetCurrentManagedThreadName (bool create)
		{
			if (create)
				return Runtime.GetCurrentManagedThreadName ();
			return null;
		}

		string? GetCurrentManagedThreadStack (bool create)
		{
			if (create)
				return Runtime.GetCurrentManagedThreadStackTrace (skipFrames: 2, fNeedFileInfo: true);
			return null;
		}

		public override void DeleteLocalReference (ref JniObjectReference reference, ref int localReferenceCount)
		{
			if (!reference.IsValid)
				return;
			JreNativeMethods.java_interop_gc_bridge_lref_log_delete (bridge,
					localReferenceCount,
					reference.Handle,
					ToByte (reference.Type),
					GetCurrentManagedThreadName (LogLocalReferenceMessages),
					Environment.CurrentManagedThreadId,
					GetCurrentManagedThreadStack (LogLocalReferenceMessages));
			base.DeleteLocalReference (ref reference, ref localReferenceCount);
		}

		public override void CreatedLocalReference (JniObjectReference reference, ref int localReferenceCount)
		{
			if (!reference.IsValid)
				return;
			base.CreatedLocalReference (reference, ref localReferenceCount);
			JreNativeMethods.java_interop_gc_bridge_lref_log_new (bridge,
					localReferenceCount,
					reference.Handle,
					ToByte (reference.Type),
					IntPtr.Zero,
					(byte) 0,
					GetCurrentManagedThreadName (LogLocalReferenceMessages),
					Environment.CurrentManagedThreadId,
					GetCurrentManagedThreadStack (LogLocalReferenceMessages));
		}

		public override IntPtr ReleaseLocalReference (ref JniObjectReference reference, ref int localReferenceCount)
		{
			if (!reference.IsValid)
				return IntPtr.Zero;
			JreNativeMethods.java_interop_gc_bridge_lref_log_delete (bridge,
					localReferenceCount,
					reference.Handle,
					ToByte (reference.Type),
					GetCurrentManagedThreadName (LogLocalReferenceMessages),
					Environment.CurrentManagedThreadId,
					GetCurrentManagedThreadStack (LogLocalReferenceMessages));
			return base.ReleaseLocalReference (ref reference, ref localReferenceCount);
		}

		public override bool LogGlobalReferenceMessages {
			get {return logGlobalRefs;}
		}

		public override void WriteGlobalReferenceLine (string format, params object?[]? args)
		{
			if (!LogGlobalReferenceMessages)
				return;
			JreNativeMethods.java_interop_gc_bridge_gref_log_message (bridge, 0, string.Format (format, args!));
			JreNativeMethods.java_interop_gc_bridge_gref_log_message (bridge, 0, "\n");
		}

		public override JniObjectReference CreateGlobalReference (JniObjectReference reference)
		{
			if (!reference.IsValid)
				return reference;
			var n   = base.CreateGlobalReference (reference);
			JreNativeMethods.java_interop_gc_bridge_gref_log_new (bridge,
					reference.Handle,
					ToByte (reference.Type),
					n.Handle,
					ToByte (n.Type),
					GetCurrentManagedThreadName (LogGlobalReferenceMessages),
					Environment.CurrentManagedThreadId,
					GetCurrentManagedThreadStack (LogGlobalReferenceMessages));
			return n;
		}

		public override void DeleteGlobalReference (ref JniObjectReference reference)
		{
			if (!reference.IsValid)
				return;
			JreNativeMethods.java_interop_gc_bridge_gref_log_delete (bridge,
					reference.Handle,
					ToByte (reference.Type),
					GetCurrentManagedThreadName (LogGlobalReferenceMessages),
					Environment.CurrentManagedThreadId,
					GetCurrentManagedThreadStack (LogGlobalReferenceMessages));
			base.DeleteGlobalReference (ref reference);
		}

		public override JniObjectReference CreateWeakGlobalReference (JniObjectReference reference)
		{
			if (!reference.IsValid)
				return reference;
			var n   = base.CreateWeakGlobalReference (reference);
			JreNativeMethods.java_interop_gc_bridge_weak_gref_log_new (bridge,
					reference.Handle,
					ToByte (reference.Type),
					n.Handle,
					ToByte (n.Type),
					GetCurrentManagedThreadName (LogGlobalReferenceMessages),
					Environment.CurrentManagedThreadId,
					GetCurrentManagedThreadStack (LogGlobalReferenceMessages));
			return n;
		}

		public override void DeleteWeakGlobalReference (ref JniObjectReference reference)
		{
			if (!reference.IsValid)
				return;
			JreNativeMethods.java_interop_gc_bridge_weak_gref_log_delete (bridge,
					reference.Handle,
					ToByte (reference.Type),
					GetCurrentManagedThreadName (LogGlobalReferenceMessages),
					Environment.CurrentManagedThreadId,
					GetCurrentManagedThreadStack (LogGlobalReferenceMessages));
			base.DeleteWeakGlobalReference (ref reference);
		}

		protected override void Dispose (bool disposing)
		{
		}

		static byte ToByte (JniObjectReferenceType type)
		{
			switch (type) {
			case JniObjectReferenceType.Global:         return (byte) 'G';
			case JniObjectReferenceType.Invalid:        return (byte) 'I';
			case JniObjectReferenceType.Local:          return (byte) 'L';
			case JniObjectReferenceType.WeakGlobal:     return (byte) 'W';
			}
			return (byte) '*';
		}
	}

	partial class JreNativeMethods {

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_get_gref_count        (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_get_weak_gref_count   (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern IntPtr java_interop_gc_bridge_lref_get_log_file  (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_lref_set_log_level    (IntPtr bridge, int level);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern void java_interop_gc_bridge_lref_log_message     (IntPtr bridge, int level,      string? message);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern void java_interop_gc_bridge_lref_log_new         (IntPtr bridge, int lref_count, IntPtr curHandle, byte curType, IntPtr newHandle, byte newType, string? thread_name, long thread_id, string? from);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern void java_interop_gc_bridge_lref_log_delete      (IntPtr bridge, int lref_count, IntPtr handle,    byte type,                                    string? thread_name, long thread_id, string? from);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern IntPtr java_interop_gc_bridge_gref_get_log_file  (IntPtr bridge);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_gref_set_log_level    (IntPtr bridge, int level);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern void java_interop_gc_bridge_gref_log_message     (IntPtr bridge, int level,      string? message);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_gref_log_new          (IntPtr bridge,                 IntPtr curHandle, byte curType, IntPtr newHandle, byte newType, string? thread_name, long thread_id, string? from);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_gref_log_delete       (IntPtr bridge,                 IntPtr handle,    byte type,                                    string? thread_name, long thread_id, string? from);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_weak_gref_log_new     (IntPtr bridge,                 IntPtr curHandle, byte curType, IntPtr newHandle, byte newType, string? thread_name, long thread_id, string? from);

		[DllImport (JavaInteropLib, CallingConvention=CallingConvention.Cdecl)]
		internal static extern int java_interop_gc_bridge_weak_gref_log_delete  (IntPtr bridge,                 IntPtr handle,    byte type,                                    string? thread_name, long thread_id, string? from);
	}
}

