using System;

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
	public sealed class JniTypeSignatureAttribute : Attribute {

		int arrayRank;

		public JniTypeSignatureAttribute (string simpleReference)
		{
			if (simpleReference == null)
				throw new ArgumentNullException (nameof (simpleReference));
			if (simpleReference.Contains ("."))
				throw new ArgumentException ("JNI type names do not contain '.', they use '/'. Are you sure you're using a JNI type name?", nameof (simpleReference));
			if (simpleReference.StartsWith ("[", StringComparison.Ordinal))
				throw new ArgumentException ("Arrays cannot be present in simple type references.", nameof (simpleReference));
			if (simpleReference.StartsWith ("L", StringComparison.Ordinal) && simpleReference.EndsWith (";", StringComparison.Ordinal))
				throw new ArgumentException ("JNI type references are not supported.", nameof (simpleReference));

			SimpleReference     = simpleReference;
		}

		public      bool        IsKeyword               {get; set;}

		public      string      SimpleReference         {get; private set;}
		public      int         ArrayRank               {
			get {return arrayRank; }
			set {
				if (value < 0)
					throw new ArgumentException ("ArrayRank cannot be less than zero.", "value");
				arrayRank = value;
			}
		}

		public      bool        GenerateJavaWrapper     {get; set;}
	}
}

