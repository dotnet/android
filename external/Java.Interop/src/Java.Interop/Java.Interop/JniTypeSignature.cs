#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
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
		public static readonly JniTypeSignature Empty;

		internal    bool        IsKeyword           {get; private set;}

		public      string?     SimpleReference     {get; private set;}
		public      int         ArrayRank           {get; private set;}

		public      bool        IsValid {
			get {return SimpleReference != null;}
		}

		public      string      QualifiedReference {
			get {
				string typename = IsKeyword
					? SimpleReference ?? throw new InvalidOperationException ()
					: "L" + SimpleReference + ";";
				return ArrayRank == 0
					? typename
					: new string ('[', ArrayRank) + typename;
			}
		}

		public      string      Name {
			get {return ArrayRank == 0 ? SimpleReference ?? throw new InvalidOperationException (): QualifiedReference;}
		}

		public JniTypeSignature (string? simpleReference, int arrayRank = 0, bool keyword = false)
		{
			if (simpleReference != null) {
				if (simpleReference.IndexOf (".", StringComparison.Ordinal) >= 0)
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
			if (SimpleReference == null)
				throw new InvalidOperationException ();
			return new JniTypeSignature (SimpleReference, ArrayRank + rank, IsKeyword);
		}

		public JniTypeSignature GetPrimitivePeerTypeSignature ()
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

		public static JniTypeSignature Parse (string signature)
		{
			JniTypeSignature r;
			var e = TryParseWithException (signature, out r);
			if (e != null)
				throw e;
			return r;
		}

		public static bool TryParse (string signature, [NotNullWhen (true)] out JniTypeSignature result)
		{
			var e = TryParseWithException (signature, out result);
			if (e != null)
				return false;
			return true;
		}

		static Exception? TryParseWithException (string signature, out JniTypeSignature result)
		{
			result  = default (JniTypeSignature);

			if (signature == null)
				return new ArgumentNullException (nameof (signature));

			int i = 0;
			int r = 0;
			var n = (string?) null;
			var k = false;
			while (i < signature.Length && signature [i] == '[') {
				i++;
				r++;
			}
			switch (signature [i]) {
			case 'B':
			case 'C':
			case 'D':
			case 'I':
			case 'F':
			case 'J':
			case 'S':
			case 'Z':
				if (signature.Length - i > 1)
					n   = signature.Substring (i);
				else {
					n   = signature [i].ToString ();
					k   = true;
				}
				break;
			case 'L':
				int s = signature.IndexOf (';', i);
				if (s >= i && s != signature.Length-1)
					return new ArgumentException (
							string.Format ("Malformed JNI type reference: trailing text after ';' in '{0}'.", signature),
							nameof (signature));
				if (i == 0) {
					n   = s > i
						? signature.Substring (i + 1, s - i - 1)
						: signature;
				} else {
					if (s < i)
						return new ArgumentException (
								string.Format ("Malformed JNI type reference; no terminating ';' for type ref: '{0}'.", signature.Substring (i)),
								nameof (signature));
					if (s != signature.Length - 1)
						return new ArgumentException (
								string.Format ("Malformed jNI type reference: invalid trailing text: '{0}'.", signature.Substring (i)),
								nameof (signature));
					n   = signature.Substring (i + 1, s - i - 1);
				}
				break;
			default:
				if (i != 0)
					return new ArgumentException (
							string.Format ("Malformed JNI type reference: found unrecognized char '{0}' in '{1}'.",
								signature [i], signature),
							nameof (signature));
				n   = signature;
				break;
			}
			int bad = n.IndexOfAny (new[]{ '.', ';' });
			if (bad >= 0)
				return new ArgumentException (
						string.Format ("Malformed JNI type reference: contains '{0}': {1}", n [bad], signature),
						nameof (signature));
			result  = new JniTypeSignature (n, r, k);
			return null;
		}

		public override int GetHashCode ()
		{
#if NETCOREAPP
			return QualifiedReference.GetHashCode (StringComparison.Ordinal);
#else
			return QualifiedReference.GetHashCode ();
#endif
		}

		public override bool Equals (object? obj)
		{
			var v = obj as JniTypeSignature?;
			if (v.HasValue)
				return Equals (v.Value);
			return false;
		}

		public bool Equals (JniTypeSignature other)
		{
			return IsKeyword == other.IsKeyword &&
				SimpleReference == other.SimpleReference &&
				ArrayRank == other.ArrayRank;
		}

		public override string ToString ()
		{
			return string.Format ("JniTypeSignature(TypeName={0} ArrayRank={1} Keyword={2})", SimpleReference, ArrayRank, IsKeyword);
		}

		public static bool operator== (JniTypeSignature lhs, JniTypeSignature rhs)
		{
			return lhs.Equals (rhs);
		}

		public static bool operator!= (JniTypeSignature lhs, JniTypeSignature rhs)
		{
			return !lhs.Equals (rhs);
		}
	}
}
