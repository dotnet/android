#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Java.Interop
{
	public sealed class JniMethodInfo
	{
		public      IntPtr  ID      {get; private set;}

		public      bool    IsStatic    {get; private set;}

		internal    JniType?    StaticRedirect;
		internal    int?        ParameterCount;

		internal    bool    IsValid {
			get {return ID != IntPtr.Zero;}
		}

#if DEBUG
		string? name, signature;
		byte* nameUtf8, signatureUtf8;
#endif  // !DEBUG

		public      string  Name {
#if DEBUG
			get {
				unsafe {
					return name ??= GetUtf8String (nameUtf8);
				}
			}
#else   // !DEBUG
			get => throw new NotSupportedException ();
#endif  // !DEBUG
		}

		public      string  Signature {
#if DEBUG
			get {
				unsafe {
					return signature ??= GetUtf8String (signatureUtf8);
				}
			}
#else   // !DEBUG
			get => throw new NotSupportedException ();
#endif  // !DEBUG
		}

		public JniMethodInfo (IntPtr methodID, bool isStatic)
		{
			ID  = methodID;

			IsStatic    = isStatic;
		}

		public JniMethodInfo (string name, string signature, IntPtr methodID, bool isStatic)
		{
			ID              = methodID;
			IsStatic        = isStatic;

#if DEBUG
			this.name       = name;
			this.signature  = signature;
#endif  // DEBUG
		}

		/// <safety>
		/// The name must address readable NUL-terminated UTF-8 memory that remains alive
		/// and unchanged through every subsequent read of this metadata.
		/// </safety>
		internal unsafe JniMethodInfo (byte* nameUtf8, string signature, IntPtr methodID, bool isStatic)
		{
			ID              = methodID;
			IsStatic        = isStatic;

#if DEBUG
			this.nameUtf8   = nameUtf8;
			this.signature  = signature;
#endif  // DEBUG
		}

		/// <safety>
		/// The name and signature must address readable NUL-terminated UTF-8 memory that remains
		/// alive and unchanged through every subsequent read of this metadata.
		/// </safety>
		internal unsafe JniMethodInfo (byte* nameUtf8, byte* signatureUtf8, IntPtr methodID, bool isStatic)
		{
			ID                  = methodID;
			IsStatic            = isStatic;

#if DEBUG
			this.nameUtf8       = nameUtf8;
			this.signatureUtf8  = signatureUtf8;
#endif  // DEBUG
		}

		/// <safety>
		/// The signature must address readable NUL-terminated UTF-8 memory that remains alive
		/// and unchanged through every subsequent read of this metadata.
		/// </safety>
		internal unsafe JniMethodInfo (string name, byte* signatureUtf8, IntPtr methodID, bool isStatic)
		{
			ID                  = methodID;
			IsStatic            = isStatic;

#if DEBUG
			this.name           = name;
			this.signatureUtf8  = signatureUtf8;
#endif  // DEBUG
		}

#if DEBUG
		/// <safety>
		/// The value must address readable NUL-terminated UTF-8 memory for the call.
		/// </safety>
		static unsafe string GetUtf8String (byte* value)
		{
			if (value == null)
				throw new NotSupportedException ();

			unsafe {
				return System.Text.Encoding.UTF8.GetString (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value));
			}
		}
#endif  // DEBUG

		public override string ToString ()
		{
#if DEBUG
			bool haveName, haveSig;
			unsafe {
				haveName = !string.IsNullOrEmpty (name) || nameUtf8 != null;
				haveSig = !string.IsNullOrEmpty (signature) || signatureUtf8 != null;
			}
#else   // DEBUG
			bool haveName   = false;
			bool haveSig    = false;
#endif  // DEBUG
			return string.Format ("JniMethodInfo({0}{1}{2}{3}ID=0x{4})",
					haveName ? "Name=" + Name : string.Empty,
					haveName ? ", " : string.Empty,
					haveSig  ? "Signature=" + Signature : string.Empty,
					haveName || haveSig ? ", " : string.Empty,
					ID.ToString ("x"));
		}
	}
}
