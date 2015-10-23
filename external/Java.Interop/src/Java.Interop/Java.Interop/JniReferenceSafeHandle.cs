using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
namespace Java.Interop
{
	abstract class JniReferenceSafeHandle : SafeHandle
	{
		public static readonly JniReferenceSafeHandle Null = new JniInvocationHandle (IntPtr.Zero);

		protected JniReferenceSafeHandle ()
			: this (ownsHandle:true)
		{
		}

		internal JniReferenceSafeHandle (bool ownsHandle)
			: base (IntPtr.Zero, ownsHandle)
		{
		}

		public override bool IsInvalid {
			get {return base.handle == IntPtr.Zero;}
		}

		public JniObjectReferenceType ReferenceType {
			get {
				if (IsInvalid)
					throw new ObjectDisposedException (GetType ().FullName);
				return JniEnvironment.References.GetObjectRefType (new JniObjectReference (this));
			}
		}

		internal IntPtr _GetAndClearHandle ()
		{
			var h   = handle;
			handle  = IntPtr.Zero;
			return h;
		}

		internal void Invalidate ()
		{
			handle = IntPtr.Zero;
		}

		public JniGlobalReference NewGlobalRef ()
		{
			var r = new JniObjectReference (DangerousGetHandle (), ReferenceType);
			return new JniGlobalReference (r.NewGlobalRef ().Handle);
		}

		public JniLocalReference NewLocalRef ()
		{
			var r = new JniObjectReference (DangerousGetHandle (), ReferenceType);
			return new JniLocalReference (r.NewLocalRef ().Handle);
		}

		public JniWeakGlobalReference NewWeakGlobalRef ()
		{
			var r = new JniObjectReference (DangerousGetHandle (), ReferenceType);
			return new JniWeakGlobalReference (r.NewWeakGlobalRef ().Handle);
		}

		internal string GetJniTypeName ()
		{
			var r = new JniObjectReference (DangerousGetHandle (), ReferenceType);
			return JniEnvironment.Types.GetJniTypeNameFromInstance (r);
		}

		public override string ToString ()
		{
			return string.Format ("{0}(0x{1})", GetType ().FullName, handle.ToString ("x"));
		}
	}

	class JniInvocationHandle : JniReferenceSafeHandle {

		public JniInvocationHandle (IntPtr handle)
			: base (ownsHandle:false)
		{
			SetHandle (handle);
		}

		protected override bool ReleaseHandle ()
		{
			return true;
		}
	}
}
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES

