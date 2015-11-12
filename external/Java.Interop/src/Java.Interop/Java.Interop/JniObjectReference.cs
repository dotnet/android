using System;
using System.Runtime.InteropServices;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES && FEATURE_JNIOBJECTREFERENCE_INTPTRS
#error  JniObjectReference cannot support both SafeHandles and IntPtrs.
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES && FEATURE_JNIOBJECTREFERENCE_INTPTRS

namespace Java.Interop
{
	[Flags]
	enum JniObjectReferenceFlags : uint {
		None,
		Alloc   = 1 << 16,
	}

	public struct JniObjectReference : IEquatable<JniObjectReference>
	{
		const   uint    FlagsMask   = 0xFFFF0000;
		const   uint    TypeMask    = 0x0000FFFF;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
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
#elif FEATURE_JNIOBJECTREFERENCE_INTPTRS
		public      IntPtr                  Handle  {get; private set;}
#endif

		uint    referenceInfo;

		public  JniObjectReferenceType      Type    {
			get {return (JniObjectReferenceType) (referenceInfo & TypeMask);}
		}

		internal    JniObjectReferenceFlags Flags {
			get {return (JniObjectReferenceFlags) (referenceInfo & FlagsMask);}
			set {referenceInfo |= (((uint) value) & FlagsMask);}
		}

		public  bool                        IsValid {
			get {
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
				return SafeHandle != null && !SafeHandle.IsInvalid && !SafeHandle.IsClosed;
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
				return Handle != IntPtr.Zero;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS
			}
		}

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
		internal JniObjectReference (JniReferenceSafeHandle handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			safeHandle      = handle;
			referenceInfo   = (uint) type;
		}
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES

		public JniObjectReference (IntPtr handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			referenceInfo   = (uint) type;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
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
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
			Handle  = handle;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS
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
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			return object.ReferenceEquals (SafeHandle, value.SafeHandle);
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
			return Handle == value.Handle;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS
		}

		public static bool operator == (JniObjectReference lhs, JniObjectReference rhs)
		{
			return lhs.Handle == rhs.Handle;
		}

		public static bool operator != (JniObjectReference lhs, JniObjectReference rhs)
		{
			return lhs.Handle != rhs.Handle;
		}

		public JniObjectReference NewGlobalRef ()
		{
			return JniEnvironment.Runtime.ObjectReferenceManager.CreateGlobalReference (this);
		}

		public JniObjectReference NewLocalRef ()
		{
			return JniEnvironment.Runtime.ObjectReferenceManager.CreateLocalReference (JniEnvironment.CurrentInfo, this);
		}

		public JniObjectReference NewWeakGlobalRef ()
		{
			return JniEnvironment.Runtime.ObjectReferenceManager.CreateWeakGlobalReference (this);
		}

		internal void Invalidate ()
		{
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			if (safeHandle != null)
				safeHandle.Invalidate ();
			safeHandle  = null;
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES

#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
			Handle      = IntPtr.Zero;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS

			referenceInfo   = 0;
		}

		public override string ToString ()
		{
			return string.Format ("JniObjectReference(Handle=0x{0}, Type={1})", Handle.ToString ("x"), Type.ToString ());
		}

		public static void Dispose (ref JniObjectReference reference)
		{
			Dispose (ref reference, JniObjectReferenceOptions.DisposeSourceReference);
		}

		const JniObjectReferenceOptions TransferMask    = JniObjectReferenceOptions.CreateNewReference | JniObjectReferenceOptions.DisposeSourceReference;

		public static void Dispose (ref JniObjectReference reference, JniObjectReferenceOptions transfer)
		{
			if (!reference.IsValid)
				return;

			switch (transfer & TransferMask) {
			case JniObjectReferenceOptions.CreateNewReference:
				break;
			case JniObjectReferenceOptions.DisposeSourceReference:
				switch (reference.Type) {
				case JniObjectReferenceType.Global:
					JniEnvironment.Runtime.ObjectReferenceManager.DeleteGlobalReference (ref reference);
					break;
				case JniObjectReferenceType.Local:
					JniEnvironment.Runtime.ObjectReferenceManager.DeleteLocalReference (JniEnvironment.CurrentInfo, ref reference);
					break;
				case JniObjectReferenceType.WeakGlobal:
					JniEnvironment.Runtime.ObjectReferenceManager.DeleteWeakGlobalReference (ref reference);
					break;
				default:
					throw new NotImplementedException ("Do not know how to dispose: " + reference.Type.ToString () + ".");
				}
				reference.Invalidate ();
				break;
			}
		}
	}
}

