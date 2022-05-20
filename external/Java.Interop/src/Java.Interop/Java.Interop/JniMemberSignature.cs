#nullable enable

#if NET

using System;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Java.Interop
{
	public struct JniMemberSignature : IEquatable<JniMemberSignature>
	{
		public   static readonly    JniMemberSignature  Empty;

		string?                 memberName;
		string?                 memberSignature;

		public      string      MemberName        => memberName ?? throw new InvalidOperationException ();
		public      string      MemberSignature   => memberSignature ?? throw new InvalidOperationException ();

		public JniMemberSignature (string memberName, string memberSignature)
		{
			if (string.IsNullOrEmpty (memberName)) {
				throw new ArgumentNullException (nameof (memberName));
			}
			if (string.IsNullOrEmpty (memberSignature)) {
				throw new ArgumentNullException (nameof (memberSignature));
			}
			this.memberName         = memberName;
			this.memberSignature    = memberSignature;
		}

		public static int GetParameterCountFromMethodSignature (string jniMethodSignature)
		{
			if (jniMethodSignature.Length < "()V".Length || jniMethodSignature [0] != '(' ) {
				throw new ArgumentException (
						$"Member signature `{jniMethodSignature}` is not a method signature.  Method signatures must start with `(`.",
						nameof (jniMethodSignature));
			}
			int count = 0;
			int index = 1;
			while (index < jniMethodSignature.Length &&
					jniMethodSignature [index] != ')') {
				ExtractType (jniMethodSignature, ref index);
				count++;
			}
			return count;
		}

		internal static (int StartIndex, int Length) ExtractType (string signature, ref int index)
		{
			AssertSignatureIndex (signature, index);
			var i = index++;
			switch (signature [i]) {
			case '[':
				if ((i+1) >= signature.Length)
					throw new InvalidOperationException ($"Missing array type after '[' at index {i} in: `{signature}`");
				var rest    = ExtractType (signature, ref index);
				return (StartIndex: i, Length: index - i);
			case 'B':
			case 'C':
			case 'D':
			case 'F':
			case 'I':
			case 'J':
			case 'S':
			case 'V':
			case 'Z':
				return (StartIndex: i, Length: 1);
			case 'L':
				int depth = 0;
				int e = index;
				while (e < signature.Length) {
					var c = signature [e++];
					if (depth == 0 && c == ';')
						break;
				}
				if (e > signature.Length)
					throw new InvalidOperationException ($"Missing reference type after `{signature [i]}` at index {i} in `{signature}`!");
				index = e;
				return (StartIndex: i, Length: (e - i));
			default:
				throw new InvalidOperationException ($"Unknown JNI Type `{signature [i]}` within: `{signature}`!");
			}
		}

		internal static void AssertSignatureIndex (string signature, int index)
		{
			if (signature == null)
				throw new ArgumentNullException (nameof (signature));
			if (signature.Length == 0)
				throw new ArgumentException ("Descriptor cannot be empty string", nameof (signature));
			if (index >= signature.Length)
				throw new ArgumentException ("index >= descriptor.Length", nameof (index));
		}

		public override int GetHashCode ()
		{
			return (memberName?.GetHashCode () ?? 0) ^
				(memberSignature?.GetHashCode () ?? 0);
		}

		public override bool Equals (object? obj)
		{
			var v = obj as JniMemberSignature?;
			if (v.HasValue)
				return Equals (v.Value);
			return false;
		}

		public bool Equals (JniMemberSignature other)
		{
			return memberName == other.memberName &&
				memberSignature == other.memberSignature;
		}

		public override string ToString ()
		{
			return $"{nameof (JniMemberSignature)} {{ " +
				$"{nameof (MemberName)} = {(memberName == null ? "null" : "\"" + memberName + "\"")}" +
				$", {nameof (MemberSignature)} = {(memberSignature == null ? "null" : "\"" + memberSignature + "\"")}" +
				$"}}";
		}

		public static bool operator== (JniMemberSignature a, JniMemberSignature b) => a.Equals (b);
		public static bool operator!= (JniMemberSignature a, JniMemberSignature b) => !a.Equals (b);
	}
}

#endif  // NET
