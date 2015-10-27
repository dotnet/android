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
	public struct JniTypeSignature : IEquatable<JniTypeSignature>
	{

		internal    bool        IsKeyword           {get; private set;}

		public      string      SimpleReference     {get; private set;}
		public      int         ArrayRank           {get; private set;}

		public      bool        IsValid {
			get {return SimpleReference != null;}
		}

		public      string      QualifiedReference {
			get {
				string typename = IsKeyword
					? SimpleReference
					: "L" + SimpleReference + ";";
				return ArrayRank == 0
					? typename
					: new string ('[', ArrayRank) + typename;
			}
		}

		public      string      Name {
			get {return ArrayRank == 0 ? SimpleReference : QualifiedReference;}
		}

		public JniTypeSignature (string simpleReference, int arrayRank = 0, bool keyword = false)
		{
			if (simpleReference != null) {
				if (simpleReference.Contains ("."))
					throw new ArgumentException ("JNI type names do not contain '.', they use '/'. Are you sure you're using a JNI type name?", nameof (simpleReference));
				if (simpleReference.StartsWith ("[", StringComparison.Ordinal))
					throw new ArgumentException ("To specify an array, use the ArrayRank property.", nameof (simpleReference));
				if (simpleReference.StartsWith ("L", StringComparison.Ordinal) && simpleReference.EndsWith (";", StringComparison.Ordinal))
					throw new ArgumentException ("JNI type references are not supported.", nameof (simpleReference));
			}

			SimpleReference = simpleReference;
			ArrayRank       = arrayRank;
			IsKeyword       = keyword;
		}

		public JniTypeSignature AddArrayRank (int rank)
		{
			return new JniTypeSignature (SimpleReference, ArrayRank + rank, IsKeyword);
		}

		public JniTypeSignature GetPrimitiveWrapper ()
		{
			if (!IsKeyword)
				return this;
			switch (SimpleReference) {
			case "V": return new JniTypeSignature ("java/lang/Void",        ArrayRank);
			case "Z": return new JniTypeSignature ("java/lang/Boolean",     ArrayRank);
			case "B": return new JniTypeSignature ("java/lang/Byte",        ArrayRank);
			case "C": return new JniTypeSignature ("java/lang/Character",   ArrayRank);
			case "S": return new JniTypeSignature ("java/lang/Short",       ArrayRank);
			case "I": return new JniTypeSignature ("java/lang/Integer",     ArrayRank);
			case "J": return new JniTypeSignature ("java/lang/Long",        ArrayRank);
			case "F": return new JniTypeSignature ("java/lang/Float",       ArrayRank);
			case "D": return new JniTypeSignature ("java/lang/Double",      ArrayRank);
			default:
				throw new InvalidOperationException (string.Format ("SimpleReference '{0}' isn't a known keyword reference, yet is a keyword.", SimpleReference));
			}
		}

		public override int GetHashCode ()
		{
			return QualifiedReference.GetHashCode ();
		}

		public override bool Equals (object value)
		{
			var v = value as JniTypeSignature?;
			if (v.HasValue)
				return Equals (v);
			return false;
		}

		public bool Equals (JniTypeSignature value)
		{
			return IsKeyword == value.IsKeyword &&
				SimpleReference == value.SimpleReference &&
				ArrayRank == value.ArrayRank;
		}

		public override string ToString ()
		{
			return string.Format ("JniTypeSignature(TypeName={0} ArrayRank={1} Keyword={2})", SimpleReference, ArrayRank, IsKeyword);
		}
	}
}
