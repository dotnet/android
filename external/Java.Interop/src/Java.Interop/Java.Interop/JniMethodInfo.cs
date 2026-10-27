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
		unsafe byte* nameUtf8, signatureUtf8;
#endif  // !DEBUG

		public      string  Name {
#if DEBUG
			get {
				unsafe {
					// SAFETY: the borrowed constructor's owner keeps the name readable and immutable through this lazy read.
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
					// SAFETY: the borrowed constructor's owner keeps the signature readable and immutable through this lazy read.
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
		/// In Debug, keep a non-null UTF-8 name readable through its NUL and unchanged through
		/// every metadata read; no alias may write or free it concurrently. No ownership is
		/// taken, including on failure. Null diagnostic names throw when read.
		/// </safety>
		internal unsafe JniMethodInfo (byte* nameUtf8, string signature, IntPtr methodID, bool isStatic)
		{
			ID              = methodID;
			IsStatic        = isStatic;

#if DEBUG
			unsafe {
				// SAFETY: retain only the address; the caller owns its storage through all later diagnostic reads.
				this.nameUtf8   = nameUtf8;
			}
			this.signature  = signature;
#endif  // DEBUG
		}

		/// <safety>
		/// In Debug, keep both non-null UTF-8 buffers readable through their NULs and unchanged
		/// through every metadata read. Read-only aliases may overlap; no concurrent writes
		/// or frees are allowed. No ownership is taken, even on failure. Null diagnostics throw.
		/// </safety>
		internal unsafe JniMethodInfo (byte* nameUtf8, byte* signatureUtf8, IntPtr methodID, bool isStatic)
		{
			ID                  = methodID;
			IsStatic            = isStatic;

#if DEBUG
			unsafe {
				// SAFETY: store caller-owned addresses without reading them; their lifetime covers later lazy diagnostics.
				this.nameUtf8       = nameUtf8;
				this.signatureUtf8  = signatureUtf8;
			}
#endif  // DEBUG
		}

		/// <safety>
		/// In Debug, keep a non-null UTF-8 signature readable through its NUL and unchanged
		/// through every metadata read; no alias may write or free it concurrently. No ownership
		/// is taken, including on failure. Null diagnostic signatures throw when read.
		/// </safety>
		internal unsafe JniMethodInfo (string name, byte* signatureUtf8, IntPtr methodID, bool isStatic)
		{
			ID                  = methodID;
			IsStatic            = isStatic;

#if DEBUG
			this.name           = name;
			unsafe {
				// SAFETY: retain only the caller-owned address, whose storage outlives every lazy signature read.
				this.signatureUtf8  = signatureUtf8;
			}
#endif  // DEBUG
		}

#if DEBUG
		/// <safety>
		/// Keep non-null UTF-8 bytes readable through their NUL and unchanged until return or
		/// exception, without writes/frees through aliases. Null throws; no storage is retained.
		/// </safety>
		static unsafe string GetUtf8String (byte* value)
		{
			if (value == null)
				throw new NotSupportedException ();

			unsafe {
				// SAFETY: the caller supplies readable immutable bytes through the terminator for this synchronous copy.
				return System.Text.Encoding.UTF8.GetString (MemoryMarshal.CreateReadOnlySpanFromNullTerminated (value));
			}
		}
#endif  // DEBUG

		public override string ToString ()
		{
#if DEBUG
			bool haveName, haveSig;
			unsafe {
				// SAFETY: these tests compare addresses only; any subsequent string read uses the constructor's lifetime contract.
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
