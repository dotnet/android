using System;

namespace Java.Interop
{
	[AttributeUsage (AttributeTargets.Class | AttributeTargets.Interface, AllowMultiple = false)]
	public sealed class JniTypeInfoAttribute : Attribute {

		int arrayRank;

		public JniTypeInfoAttribute (string jniTypeName)
		{
			if (jniTypeName == null)
				throw new ArgumentNullException ("jniTypeName");
			if (jniTypeName.Contains ("."))
				throw new ArgumentException ("JNI type names do not contain '.', they use '/'. Are you sure you're using a JNI type name?", "jniTypeName");
			if (jniTypeName.StartsWith ("[", StringComparison.Ordinal))
				throw new ArgumentException ("To specify an array, use the ArrayRank property.", "jniTypeName");
			if (jniTypeName.StartsWith ("L", StringComparison.Ordinal) && jniTypeName.EndsWith (";", StringComparison.Ordinal))
				throw new ArgumentException ("JNI type references are not supported.", "jniTypeName");

			JniTypeName = jniTypeName;
		}

		public  string  JniTypeName     {get; private set;}
		public  bool    TypeIsKeyword   {get; set;}
		public  int     ArrayRank {
			get {return arrayRank;}
			set {
				if (value < 0)
					throw new ArgumentException ("ArrayRank cannot be less than zero.", "value");
				arrayRank = value;
			}
		}

		public override string ToString ()
		{
			string typename = JniTypeName;
			if (ArrayRank > 0 && !TypeIsKeyword)
				typename = "L" + typename + ";";
			return ArrayRank == 0
				? typename
				: new string ('[', ArrayRank) + typename;
		}
	}
}

