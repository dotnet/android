#nullable enable

using System;
using System.Runtime.CompilerServices;

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

		public      IntPtr                  Handle  {
			[MethodImpl (MethodImplOptions.AggressiveInlining)]
			get; 
			private set;
		}

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
				return Handle != IntPtr.Zero;
			}
		}

		public JniObjectReference (IntPtr handle, JniObjectReferenceType type = JniObjectReferenceType.Invalid)
		{
			referenceInfo   = (uint) type;
			Handle  = handle;
		}

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
			return Handle == other.Handle;
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
			Handle      = IntPtr.Zero;

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
			case JniObjectReferenceType.Invalid:
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
