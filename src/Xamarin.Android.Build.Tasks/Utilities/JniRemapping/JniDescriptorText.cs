#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.Android.Tasks.JniRemapping
{
	/// <summary>
	/// Small, self-contained helpers for scanning and rewriting JNI type names, descriptors, and
	/// the various encoded string forms that Java.Interop / the generator embed in compiled
	/// assemblies (JniPeerMembers "name.descriptor" ids, RegisterNatives lines, etc).
	///
	/// These deliberately duplicate a subset of Microsoft.Android.Sdk.TrimmableTypeMap's
	/// JniSignatureHelper (which is internal to that assembly and not visible here) rather than
	/// exposing it across assembly boundaries.
	/// </summary>
	static class JniDescriptorText
	{
		/// <summary>
		/// Rewrites every embedded reference-type name ("Lfoo/bar/Baz;") within a JNI type or
		/// method descriptor using <paramref name="renameClass"/>. Returns false (and the
		/// original text unmodified) if nothing needed to change.
		/// </summary>
		public static bool TryRewriteDescriptor (string descriptor, Func<string, string?> renameClass, out string rewritten)
		{
			var sb = new StringBuilder (descriptor.Length);
			bool changed = false;
			int i = 0;
			while (i < descriptor.Length) {
				int start = i;
				if (!TryScanSingleToken (descriptor, ref i, allowParens: true)) {
					// Not a type token (e.g. '(' or ')' bracket of a method descriptor) - copy verbatim.
					sb.Append (descriptor [start]);
					i = start + 1;
					continue;
				}

				string token = descriptor.Substring (start, i - start);
				if (TryRewriteSingleTypeToken (token, renameClass, out string newToken)) {
					changed = true;
					sb.Append (newToken);
				} else {
					sb.Append (token);
				}
			}

			rewritten = changed ? sb.ToString () : descriptor;
			return changed;
		}

		/// <summary>
		/// Rewrites a single JNI type token ("I", "[I", "Lfoo/Bar;", "[Lfoo/Bar;", ...) if it is an
		/// object/array-of-object reference whose class name has a rename entry.
		/// </summary>
		static bool TryRewriteSingleTypeToken (string token, Func<string, string?> renameClass, out string rewritten)
		{
			rewritten = token;
			int arrayDepth = 0;
			while (arrayDepth < token.Length && token [arrayDepth] == '[') {
				arrayDepth++;
			}

			if (arrayDepth >= token.Length || token [arrayDepth] != 'L') {
				return false; // Primitive (or malformed) - nothing to rename.
			}

			// token[arrayDepth] == 'L', token ends with ';'.
			string className = token.Substring (arrayDepth + 1, token.Length - arrayDepth - 2);
			string? renamed = renameClass (className);
			if (renamed == null || renamed == className) {
				return false;
			}

			rewritten = token.Substring (0, arrayDepth) + "L" + renamed + ";";
			return true;
		}

		/// <summary>
		/// Scans a single JNI type descriptor token ("I", "[[I", "Lfoo/Bar;", ...) starting at
		/// <paramref name="i"/>, advancing <paramref name="i"/> past it. Returns false (without
		/// advancing) if the character at <paramref name="i"/> cannot start a type token.
		/// </summary>
		static bool TryScanSingleToken (string s, ref int i, bool allowParens)
		{
			int start = i;
			int j = i;
			while (j < s.Length && s [j] == '[') {
				j++;
			}

			if (j >= s.Length) {
				return false;
			}

			switch (s [j]) {
			case 'V': case 'Z': case 'B': case 'C': case 'S': case 'I': case 'J': case 'F': case 'D':
				i = j + 1;
				return true;
			case 'L':
				int end = s.IndexOf (';', j + 1);
				if (end < 0) {
					return false;
				}
				i = end + 1;
				return true;
			default:
				return false;
			}
		}

		/// <summary>
		/// Parses a JNI method descriptor "(param1param2...)ret" into its parameter type tokens
		/// and return type token.
		/// </summary>
		public static bool TryParseMethodDescriptor (string descriptor, out List<string> parameterTypes, out string returnType)
		{
			parameterTypes = new List<string> ();
			returnType = "";

			if (descriptor.Length == 0 || descriptor [0] != '(') {
				return false;
			}

			int i = 1;
			while (i < descriptor.Length && descriptor [i] != ')') {
				int start = i;
				if (!TryScanSingleToken (descriptor, ref i, allowParens: false)) {
					return false;
				}
				parameterTypes.Add (descriptor.Substring (start, i - start));
			}

			if (i >= descriptor.Length || descriptor [i] != ')') {
				return false;
			}
			i++;

			int retStart = i;
			if (!TryScanSingleToken (descriptor, ref i, allowParens: false) || i != descriptor.Length) {
				return false;
			}

			returnType = descriptor.Substring (retStart);
			return true;
		}

		/// <summary>
		/// True if <paramref name="descriptor"/> is a syntactically valid JNI method descriptor,
		/// e.g. "(Ljava/lang/Object;)Z" or "()V".
		/// </summary>
		public static bool IsValidMethodDescriptor (string descriptor)
			=> TryParseMethodDescriptor (descriptor, out _, out _);

		/// <summary>
		/// True if <paramref name="descriptor"/> is a single, complete JNI field/type descriptor,
		/// e.g. "I", "[I", or "Ljava/lang/Object;" (and nothing else follows it).
		/// </summary>
		public static bool IsValidFieldDescriptor (string descriptor)
		{
			int i = 0;
			return descriptor.Length > 0 && TryScanSingleToken (descriptor, ref i, allowParens: false) && i == descriptor.Length;
		}

		/// <summary>
		/// Converts a JNI type token ("I", "[Lfoo/Bar;", "Ljava/lang/String;") to its Java *source*
		/// form ("int", "foo.Bar[]", "java.lang.String") as used in mapping.txt member lines.
		/// </summary>
		public static string JniTypeTokenToJavaSource (string token)
		{
			int arrayDepth = 0;
			while (arrayDepth < token.Length && token [arrayDepth] == '[') {
				arrayDepth++;
			}

			string elementJavaName = token [arrayDepth] switch {
				'V' => "void",
				'Z' => "boolean",
				'B' => "byte",
				'C' => "char",
				'S' => "short",
				'I' => "int",
				'J' => "long",
				'F' => "float",
				'D' => "double",
				'L' => token.Substring (arrayDepth + 1, token.Length - arrayDepth - 2).Replace ('/', '.'),
				_ => throw new ArgumentException ($"Malformed JNI type token '{token}'.", nameof (token)),
			};

			return elementJavaName + string.Concat (System.Linq.Enumerable.Repeat ("[]", arrayDepth));
		}

		/// <summary>
		/// Splits a JNI method descriptor's parameter list into Java-source-form parameter types,
		/// e.g. "(Landroid/os/Bundle;I)V" -&gt; ["android.os.Bundle", "int"].
		/// </summary>
		public static List<string> MethodDescriptorToJavaParameterTypes (string descriptor)
		{
			if (!TryParseMethodDescriptor (descriptor, out var parameterTypes, out _)) {
				throw new ArgumentException ($"Malformed JNI method descriptor '{descriptor}'.", nameof (descriptor));
			}

			var result = new List<string> (parameterTypes.Count);
			foreach (string p in parameterTypes) {
				result.Add (JniTypeTokenToJavaSource (p));
			}
			return result;
		}
	}
}
