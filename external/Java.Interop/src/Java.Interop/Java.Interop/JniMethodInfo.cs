#nullable enable

using System;

namespace Java.Interop
{
	public sealed class JniMethodInfo
	{
		public      IntPtr  ID      {get; private set;}

		public      bool    IsStatic    {get; private set;}

#if NET
		internal    JniType?    StaticRedirect;
		internal    int?        ParameterCount;
#endif  //NET

		internal    bool    IsValid {
			get {return ID != IntPtr.Zero;}
		}

#if DEBUG
		string? name, signature;
#endif  // !DEBUG

		public      string  Name {
#if DEBUG
			get => name ?? throw new NotSupportedException ();
#else   // !DEBUG
			get => throw new NotSupportedException ();
#endif  // !DEBUG
		}

		public      string  Signature {
#if DEBUG
			get => signature ?? throw new NotSupportedException ();
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

		public override string ToString ()
		{
#if DEBUG
			bool haveName   = !string.IsNullOrEmpty (name);
			bool haveSig    = !string.IsNullOrEmpty (signature);
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

