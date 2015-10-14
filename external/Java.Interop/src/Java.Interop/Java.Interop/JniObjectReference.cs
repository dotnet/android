using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	enum JniObjectReferenceFlags : uint {
		None,
		Alloc   = 1 << 16,
	}

	public struct JniObjectReference : IEquatable<JniObjectReference>
	{
		const   uint    FlagsMask   = 0xFFFF0000;
		const   uint    TypeMask    = 0x0000FFFF;

#if FEATURE_HANDLES_ARE_SAFE_HANDLES
		JniReferenceSafeHandle  safeHandle;
		internal    JniReferenceSafeHandle  SafeHandle  {
			get {return safeHandle ?? JniReferenceSafeHandle.Null;}
		}
		public      IntPtr                  Handle  {
			get {
				var h = safeHandle;
				return h == null
					? IntPtr.Zero
					: h.DangerousGetHandle ();
			}
		}
#elif FEATURE_HANDLES_ARE_INTPTRS
		public      IntPtr                  Handle  {get; private set;}
#endif

		uint    referenceInfo;

		public  JniObjectReferenceType      Type    {
			get {return (JniObjectReferenceType) (referenceInfo & TypeMask);}
			private set {referenceInfo = (uint) value;}
		}

		internal    JniObjectReferenceFlags Flags {
			get {return (JniObjectReferenceFlags) (referenceInfo & FlagsMask);}
			set {referenceInfo |= (((uint) value) & FlagsMask);}
		}

		public  bool                        IsValid {
			get {
#if FEATURE_HANDLES_ARE_SAFE_HANDLES
				return SafeHandle != null && !SafeHandle.IsInvalid && !SafeHandle.IsClosed;
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES
#if FEATURE_HANDLES_ARE_INTPTRS
				return Handle == IntPtr.Zero;
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES
			}
		}

#if FEATURE_HANDLES_ARE_SAFE_HANDLES
		internal JniObjectReference (JniReferenceSafeHandle handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			safeHandle      = handle;
			referenceInfo   = (uint) type;
		}
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES

		public JniObjectReference (IntPtr handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			referenceInfo   = (uint) type;

#if FEATURE_HANDLES_ARE_SAFE_HANDLES
			if (handle == IntPtr.Zero) {
				safeHandle = JniReferenceSafeHandle.Null;
				return;
			}
			switch (type) {
			case JniObjectReferenceType.Local:
				safeHandle  = new JniLocalReference (handle);
				break;
			case JniObjectReferenceType.Global:
				safeHandle  = new JniGlobalReference (handle);
				break;
			case JniObjectReferenceType.WeakGlobal:
				safeHandle  = new JniWeakGlobalReference (handle);
				break;
			default:
				safeHandle  = new JniInvocationHandle (handle);
				break;
			}
#elif FEATURE_HANDLES_ARE_INTPTRS
			Handle  = handle;
#endif
		}

		public override int GetHashCode ()
		{
			return Handle.GetHashCode ();
		}

		public override bool Equals (object value)
		{
			var o = value as JniObjectReference?;
			if (o.HasValue)
				return Equals (o.Value);
			return false;
		}

		public bool Equals (JniObjectReference value)
		{
#if FEATURE_HANDLES_ARE_SAFE_HANDLES
			return object.ReferenceEquals (SafeHandle, value.SafeHandle);
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES
#if FEATURE_HANDLES_ARE_INTPTRS
			return Handle == value.Handle;
#endif  // FEATURE_HANDLES_ARE_INTPTRS
		}

		public JniObjectReference NewGlobalRef ()
		{
			return JniEnvironment.Current.JavaVM.JniHandleManager.CreateGlobalReference (this);
		}

		public JniObjectReference NewLocalRef ()
		{
			return JniEnvironment.Current.JavaVM.JniHandleManager.CreateLocalReference (JniEnvironment.Current, this);
		}

		public JniObjectReference NewWeakGlobalRef ()
		{
			return JniEnvironment.Current.JavaVM.JniHandleManager.CreateWeakGlobalReference (this);
		}

		internal void Invalidate ()
		{
#if FEATURE_HANDLES_ARE_SAFE_HANDLES
			if (safeHandle != null)
				safeHandle.Invalidate ();
			safeHandle  = null;
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES

#if FEATURE_HANDLES_ARE_INTPTRS
			Handle      = IntPtr.Zero;
#endif  // FEATURE_HANDLES_ARE_SAFE_HANDLES

			referenceInfo   = 0;
		}

		public override string ToString ()
		{
			return string.Format ("JniObjectReference(Handle=0x{0}, Type={1})", Handle.ToString ("x"), Type.ToString ());
		}
	}
}

