#nullable enable

using System;
using System.Runtime.CompilerServices;
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

	public partial struct JniObjectReference : IEquatable<JniObjectReference>
	{
		const   uint    FlagsMask   = 0xFFFF0000;
		const   uint    TypeMask    = 0x0000FFFF;

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
		GCHandle                gcHandle;
		internal    JniReferenceSafeHandle  SafeHandle  {
			get {return gcHandle.IsAllocated ? ((JniReferenceSafeHandle) gcHandle.Target) : JniReferenceSafeHandle.Null;}
		}
		public      IntPtr                  Handle  {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get {
				var h = SafeHandle;
				return h == null
					? IntPtr.Zero
					: h.DangerousGetHandle ();
			}
		}
#elif FEATURE_JNIOBJECTREFERENCE_INTPTRS
		public      IntPtr                  Handle  {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get; 
			private set;
		}
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
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
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
			this.gcHandle   = GCHandle.Alloc (handle, GCHandleType.Normal);
			referenceInfo   = (uint) type;
		}
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES

		public JniObjectReference (IntPtr handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			: this (FromIntPtr (handle, type), type)
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
		{
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
			referenceInfo   = (uint) type;
			Handle  = handle;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS
		}

#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
		static JniReferenceSafeHandle FromIntPtr (IntPtr handle, JniObjectReferenceType type)
		{
			if (handle == IntPtr.Zero) {
				return JniReferenceSafeHandle.Null;
			}
			switch (type) {
			case JniObjectReferenceType.Local:      return new JniLocalReference (handle);
			case JniObjectReferenceType.Global:     return new JniGlobalReference (handle);
			case JniObjectReferenceType.WeakGlobal: return new JniWeakGlobalReference (handle);
			default:
				return new JniInvocationHandle (handle);
			}
		}
#endif  // #if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES

		public override int GetHashCode ()
		{
			return Handle.GetHashCode ();
		}

		public override bool Equals (object? obj)
		{
			var o = obj as JniObjectReference?;
			if (o.HasValue)
				return Equals (o.Value);
			return false;
		}

		public bool Equals (JniObjectReference other)
		{
#if FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
			return object.ReferenceEquals (SafeHandle, other.SafeHandle);
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES
#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
			return Handle == other.Handle;
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
			var s = SafeHandle;
			if (s != null)
				s.Invalidate ();
			gcHandle.Free ();
#endif  // FEATURE_JNIOBJECTREFERENCE_SAFEHANDLES

#if FEATURE_JNIOBJECTREFERENCE_INTPTRS
			Handle      = IntPtr.Zero;
#endif  // FEATURE_JNIOBJECTREFERENCE_INTPTRS

			referenceInfo   = 0;
		}

		public override string ToString ()
		{
			return "0x" + Handle.ToString ("x") + "/" + ToString (Type);
		}


		static string ToString (JniObjectReferenceType type)
		{
			switch (type) {
			case JniObjectReferenceType.Global:         return "G";
			case JniObjectReferenceType.Invalid:        return "I";
			case JniObjectReferenceType.Local:          return "L";
			case JniObjectReferenceType.WeakGlobal:     return "W";
			}
			return type.ToString ();
		}

		public static void Dispose (ref JniObjectReference reference)
		{
			if (!reference.IsValid)
				return;

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
		}

		public static void Dispose (ref JniObjectReference reference, JniObjectReferenceOptions options)
		{
			if (options == JniObjectReferenceOptions.None)
				return;

			if (!reference.IsValid)
				return;

			if ((options & DisposeSource) == 0)
				return;

			Dispose (ref reference);
		}
	}
}

