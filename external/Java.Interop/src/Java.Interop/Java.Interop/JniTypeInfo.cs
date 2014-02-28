using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace Java.Interop
{
	public struct JniTypeInfo {

		int     arrayRank;

		public string JniTypeName { get; set; }

		public int ArrayRank {
			get {return arrayRank;}
			set {
				if (value < 0)
					throw new ArgumentException ("ArrayRank cannot be less than zero.", "value");
				arrayRank = value;
			}
		}

		public bool TypeIsKeyword { get; set; }

		public string JniTypeReference {
			get {
				string typename = TypeIsKeyword
					? JniTypeName
					: "L" + JniTypeName + ";";
				return ArrayRank == 0
					? typename
					: new string ('[', ArrayRank) + typename;
			}
		}

		public JniTypeInfo (string jniTypeName, bool typeIsKeyword = false, int arrayRank = 0)
		{
			if (jniTypeName != null && jniTypeName.Contains ("."))
				throw new ArgumentException ("JNI type names do not contain '.', they use '/'. Are you sure you're using a JNI type name?", "jniTypeName");
			if (jniTypeName != null && jniTypeName.StartsWith ("[", StringComparison.Ordinal))
				throw new ArgumentException ("To specify an array, use the ArrayRank property.", "jniTypeName");
			if (jniTypeName != null && jniTypeName.StartsWith ("L", StringComparison.Ordinal) && jniTypeName.EndsWith (";", StringComparison.Ordinal))
				throw new ArgumentException ("JNI type references are not supported.", "jniTypeName");

			this = new JniTypeInfo ();

			JniTypeName     = jniTypeName;
			ArrayRank       = arrayRank;
			TypeIsKeyword   = typeIsKeyword;
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
