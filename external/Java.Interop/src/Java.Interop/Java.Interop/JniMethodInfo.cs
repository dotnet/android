#nullable enable

using System;

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
		IntPtr nameUtf8, signatureUtf8;
#endif  // !DEBUG

		public      string  Name {
#if DEBUG
			get => name ??= GetUtf8String (nameUtf8);
#else   // !DEBUG
			get => throw new NotSupportedException ();
#endif  // !DEBUG
		}

		public      string  Signature {
#if DEBUG
			get => signature ??= GetUtf8String (signatureUtf8);
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

		internal JniMethodInfo (IntPtr nameUtf8, string signature, IntPtr methodID, bool isStatic)
		{
			ID              = methodID;
			IsStatic        = isStatic;

#if DEBUG
			this.nameUtf8   = nameUtf8;
			this.signature  = signature;
#endif  // DEBUG
		}

		internal JniMethodInfo (IntPtr nameUtf8, IntPtr signatureUtf8, IntPtr methodID, bool isStatic)
		{
			ID                  = methodID;
			IsStatic            = isStatic;

#if DEBUG
			this.nameUtf8       = nameUtf8;
			this.signatureUtf8  = signatureUtf8;
#endif  // DEBUG
		}

		internal JniMethodInfo (string name, IntPtr signatureUtf8, IntPtr methodID, bool isStatic)
		{
			ID                  = methodID;
			IsStatic            = isStatic;

#if DEBUG
			this.name           = name;
			this.signatureUtf8  = signatureUtf8;
#endif  // DEBUG
		}

#if DEBUG
		static unsafe string GetUtf8String (IntPtr value)
		{
			if (value == IntPtr.Zero)
				throw new NotSupportedException ();

			byte* start = (byte*)value;
			int length = 0;
			while (start [length] != 0)
				length++;
			return System.Text.Encoding.UTF8.GetString (start, length);
		}
#endif  // DEBUG

		public override string ToString ()
		{
#if DEBUG
			bool haveName   = !string.IsNullOrEmpty (name) || nameUtf8 != IntPtr.Zero;
			bool haveSig    = !string.IsNullOrEmpty (signature) || signatureUtf8 != IntPtr.Zero;
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
