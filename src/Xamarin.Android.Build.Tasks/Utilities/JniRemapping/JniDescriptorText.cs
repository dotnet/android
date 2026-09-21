#nullable enable

using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.Android.Tasks.JniRemapping
{
	static class JniDescriptorText
	{
		public static bool TryRewriteDescriptor (string descriptor, Func<string, string?> renameClass, out string rewritten)
		{
			var sb = new StringBuilder (descriptor.Length);
			bool changed = false;
			int i = 0;
			while (i < descriptor.Length) {
				int start = i;
				if (!TryScanSingleToken (descriptor, ref i, allowVoid: true)) {
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

		static bool TryRewriteSingleTypeToken (string token, Func<string, string?> renameClass, out string rewritten)
		{
			rewritten = token;
			int arrayDepth = 0;
			while (arrayDepth < token.Length && token [arrayDepth] == '[') {
				arrayDepth++;
			}

			if (arrayDepth >= token.Length || token [arrayDepth] != 'L') {
				return false;
			}

			string className = token.Substring (arrayDepth + 1, token.Length - arrayDepth - 2);
			string? renamed = renameClass (className);
			if (renamed == null || renamed == className) {
				return false;
			}

			rewritten = token.Substring (0, arrayDepth) + "L" + renamed + ";";
			return true;
		}

		static bool TryScanSingleToken (string s, ref int i, bool allowVoid)
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
			case 'V':
				if (!allowVoid || j != start) {
					return false;
				}
				i = j + 1;
				return true;
			case 'Z':
			case 'B':
			case 'C':
			case 'S':
			case 'I':
			case 'J':
			case 'F':
			case 'D':
				i = j + 1;
				return true;
			case 'L':
				int end = s.IndexOf (';', j + 1);
				if (end < 0 || !IsValidJniClassName (s, j + 1, end)) {
					return false;
				}
				i = end + 1;
				return true;
			default:
				return false;
			}
		}

		static bool IsValidJniClassName (string value, int start, int end)
		{
			if (start == end) {
				return false;
			}

			bool segmentHasCharacters = false;
			for (int i = start; i < end; i++) {
				switch (value [i]) {
				case '/':
					if (!segmentHasCharacters) {
						return false;
					}
					segmentHasCharacters = false;
					break;
				case '.':
				case '[':
					return false;
				default:
					segmentHasCharacters = true;
					break;
				}
			}
			return segmentHasCharacters;
		}

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
				if (!TryScanSingleToken (descriptor, ref i, allowVoid: false)) {
					return false;
				}
				parameterTypes.Add (descriptor.Substring (start, i - start));
			}

			if (i >= descriptor.Length || descriptor [i] != ')') {
				return false;
			}
			i++;

			int retStart = i;
			if (!TryScanSingleToken (descriptor, ref i, allowVoid: true) || i != descriptor.Length) {
				return false;
			}

			returnType = descriptor.Substring (retStart);
			return true;
		}

		public static bool IsValidMethodDescriptor (string descriptor)
			=> TryParseMethodDescriptor (descriptor, out _, out _);

		public static bool IsValidFieldDescriptor (string descriptor)
		{
			int i = 0;
			return descriptor.Length > 0 && TryScanSingleToken (descriptor, ref i, allowVoid: false) && i == descriptor.Length;
		}

		public static string JniTypeTokenToJavaSource (string token)
		{
			int tokenEnd = 0;
			if (!TryScanSingleToken (token, ref tokenEnd, allowVoid: true) || tokenEnd != token.Length) {
				throw new ArgumentException ($"Malformed JNI type token '{token}'.", nameof (token));
			}

			int arrayDepth = 0;
			while (arrayDepth < token.Length && token [arrayDepth] == '[') {
				arrayDepth++;
			}

			return JniTypeTokenToJavaSource (token, arrayDepth);
		}

		static string JniTypeTokenToJavaSource (string token, int arrayDepth)
		{
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

			if (arrayDepth == 0) {
				return elementJavaName;
			}

			var result = new StringBuilder (elementJavaName.Length + arrayDepth * 2);
			result.Append (elementJavaName);
			for (int i = 0; i < arrayDepth; i++) {
				result.Append ("[]");
			}
			return result.ToString ();
		}

		public static List<string> MethodDescriptorToJavaParameterTypes (string descriptor)
		{
			MethodDescriptorToJavaTypes (descriptor, out var parameterTypes, out _);
			return parameterTypes;
		}

		public static void MethodDescriptorToJavaTypes (string descriptor, out List<string> parameterTypes, out string returnType)
		{
			if (!TryParseMethodDescriptor (descriptor, out var jniParameterTypes, out string jniReturnType)) {
				throw new ArgumentException ($"Malformed JNI method descriptor '{descriptor}'.", nameof (descriptor));
			}

			parameterTypes = new List<string> (jniParameterTypes.Count);
			foreach (string parameterType in jniParameterTypes) {
				int arrayDepth = 0;
				while (parameterType [arrayDepth] == '[') {
					arrayDepth++;
				}
				parameterTypes.Add (JniTypeTokenToJavaSource (parameterType, arrayDepth));
			}
			int returnArrayDepth = 0;
			while (jniReturnType [returnArrayDepth] == '[') {
				returnArrayDepth++;
			}
			returnType = JniTypeTokenToJavaSource (jniReturnType, returnArrayDepth);
		}

		public static string JavaSourceTypeToJniTypeToken (string javaSourceType)
		{
			string trimmed = javaSourceType.Trim ();
			int arrayDepth = 0;
			int elementEnd = trimmed.Length;
			while (elementEnd >= 2 &&
					trimmed [elementEnd - 1] == ']' &&
					trimmed [elementEnd - 2] == '[') {
				arrayDepth++;
				elementEnd -= 2;
			}

			string elementType = trimmed.Substring (0, elementEnd).Trim ();
			if (elementType.Length == 0) {
				throw new ArgumentException ($"Malformed Java source type '{javaSourceType}'.", nameof (javaSourceType));
			}

			string elementToken = elementType switch {
				"void" => "V",
				"boolean" => "Z",
				"byte" => "B",
				"char" => "C",
				"short" => "S",
				"int" => "I",
				"long" => "J",
				"float" => "F",
				"double" => "D",
				_ => "L" + elementType.Replace ('.', '/') + ";",
			};

			return arrayDepth == 0 ? elementToken : new string ('[', arrayDepth) + elementToken;
		}

		public static string JavaSourceTypesToMethodDescriptor (IReadOnlyList<string> javaParameterTypes, string javaReturnType)
		{
			var descriptor = new StringBuilder ();
			descriptor.Append ('(');
			foreach (string javaParameterType in javaParameterTypes) {
				descriptor.Append (JavaSourceTypeToJniTypeToken (javaParameterType));
			}
			descriptor.Append (')');
			descriptor.Append (JavaSourceTypeToJniTypeToken (javaReturnType));
			return descriptor.ToString ();
		}
	}
}
