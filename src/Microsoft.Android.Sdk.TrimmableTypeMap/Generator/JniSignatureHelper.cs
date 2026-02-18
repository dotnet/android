using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// JNI primitive type kinds used for mapping JNI signatures → CLR types.
/// </summary>
enum JniParamKind
{
Void,     // V
Boolean,  // Z → sbyte
Byte,     // B → sbyte
Char,     // C → char
Short,    // S → short
Int,      // I → int
Long,     // J → long
Float,    // F → float
Double,   // D → double
Object,   // L...; or [ → IntPtr
}

/// <summary>
/// Helpers for parsing JNI method signatures.
/// </summary>
static class JniSignatureHelper
{
	/// <summary>
	/// Parses the parameter types from a JNI method signature like "(Landroid/os/Bundle;)V".
	/// </summary>
	public static List<JniParamKind> ParseParameterTypes (string jniSignature)
	{
		var result = new List<JniParamKind> ();
		int i = 1; // skip opening '('
		while (i < jniSignature.Length && jniSignature [i] != ')') {
			result.Add (ParseSingleType (jniSignature, ref i));
		}
		return result;
	}

	/// <summary>

	/// Parses the raw JNI type descriptor strings from a JNI method signature.

	/// </summary>
	public static List<string> ParseParameterTypeStrings (string jniSignature)
	{
		var result = new List<string> ();
		int i = 1; // skip opening '('
		while (i < jniSignature.Length && jniSignature [i] != ')') {
			int start = i;
			ParseSingleType (jniSignature, ref i);
			result.Add (jniSignature.Substring (start, i - start));
		}
		return result;
	}

	/// <summary>

	/// Extracts the return type descriptor from a JNI method signature.

	/// </summary>
	public static string ParseReturnTypeString (string jniSignature)
	{
		int i = jniSignature.IndexOf (')') + 1;
		return jniSignature.Substring (i);
	}

	/// <summary>

	/// Parses the return type from a JNI method signature.

	/// </summary>
	public static JniParamKind ParseReturnType (string jniSignature)
	{
		int i = jniSignature.IndexOf (')') + 1;
		return ParseSingleType (jniSignature, ref i);
	}

	static JniParamKind ParseSingleType (string sig, ref int i)
	{
		switch (sig [i]) {
		case 'V': i++; return JniParamKind.Void;
		case 'Z': i++; return JniParamKind.Boolean;
		case 'B': i++; return JniParamKind.Byte;
		case 'C': i++; return JniParamKind.Char;
		case 'S': i++; return JniParamKind.Short;
		case 'I': i++; return JniParamKind.Int;
		case 'J': i++; return JniParamKind.Long;
		case 'F': i++; return JniParamKind.Float;
		case 'D': i++; return JniParamKind.Double;
		case 'L':
			i = sig.IndexOf (';', i) + 1;
			return JniParamKind.Object;
		case '[':
			i++;
			ParseSingleType (sig, ref i); // skip element type
			return JniParamKind.Object;
		default:
			throw new ArgumentException ($"Unknown JNI type character '{sig [i]}' in '{sig}' at index {i}");
		}
	}

	/// <summary>

	/// Encodes the CLR type for a JNI parameter kind into a signature type encoder.

	/// </summary>
	public static void EncodeClrType (SignatureTypeEncoder encoder, JniParamKind kind)
	{
		switch (kind) {
		case JniParamKind.Boolean: encoder.Boolean (); break;
		case JniParamKind.Byte:    encoder.SByte (); break;
		case JniParamKind.Char:    encoder.Char (); break;
		case JniParamKind.Short:   encoder.Int16 (); break;
		case JniParamKind.Int:     encoder.Int32 (); break;
		case JniParamKind.Long:    encoder.Int64 (); break;
		case JniParamKind.Float:   encoder.Single (); break;
		case JniParamKind.Double:  encoder.Double (); break;
		case JniParamKind.Object:  encoder.IntPtr (); break;
		default: throw new ArgumentException ($"Cannot encode JNI param kind {kind} as CLR type");
		}
	}

	// ---- JNI name / Java source name helpers ----

	/// <summary>
	/// Validates that a JNI type name has the expected structure (e.g., "com/example/MyClass").
	/// </summary>
	internal static void ValidateJniName (string jniName)
	{
		if (string.IsNullOrEmpty (jniName)) {
			throw new ArgumentException ("JNI name must not be null or empty.", nameof (jniName));
		}

		int segmentStart = 0;
		for (int i = 0; i <= jniName.Length; i++) {
			if (i == jniName.Length || jniName [i] == '/') {
				if (i == segmentStart) {
					throw new ArgumentException ($"JNI name '{jniName}' has an empty segment.", nameof (jniName));
				}

				// First char of a segment must not be a digit
				char first = jniName [segmentStart];
				if (first >= '0' && first <= '9') {
					throw new ArgumentException ($"JNI name '{jniName}' has a segment starting with a digit.", nameof (jniName));
				}

				// All chars in the segment must be valid Java identifier chars
				for (int j = segmentStart; j < i; j++) {
					char c = jniName [j];
					bool valid = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
					             (c >= '0' && c <= '9') || c == '_' || c == '$';
					if (!valid) {
						throw new ArgumentException ($"JNI name '{jniName}' contains invalid character '{c}'.", nameof (jniName));
					}
				}

				segmentStart = i + 1;
			}
		}
	}

	/// <summary>
	/// Converts a JNI type name to a Java source type name.
	/// e.g., "android/app/Activity" \u2192 "android.app.Activity"
	/// </summary>
	internal static string JniNameToJavaName (string jniName)
	{
		return jniName.Replace ('/', '.');
	}

	/// <summary>
	/// Extracts the Java package name from a JNI type name.
	/// e.g., "com/example/MainActivity" \u2192 "com.example"
	/// Returns null for types without a package.
	/// </summary>
	internal static string? GetJavaPackageName (string jniName)
	{
		int lastSlash = jniName.LastIndexOf ('/');
		if (lastSlash < 0) {
			return null;
		}
		return jniName.Substring (0, lastSlash).Replace ('/', '.');
	}

	/// <summary>
	/// Extracts the simple Java class name from a JNI type name.
	/// e.g., "com/example/MainActivity" \u2192 "MainActivity"
	/// e.g., "com/example/Outer$Inner" \u2192 "Outer$Inner" (preserves nesting separator)
	/// </summary>
	internal static string GetJavaSimpleName (string jniName)
	{
		int lastSlash = jniName.LastIndexOf ('/');
		return lastSlash >= 0 ? jniName.Substring (lastSlash + 1) : jniName;
	}

	/// <summary>
	/// Converts a JNI type descriptor to a Java source type.
	/// e.g., "V" \u2192 "void", "I" \u2192 "int", "Landroid/os/Bundle;" \u2192 "android.os.Bundle"
	/// </summary>
	internal static string JniTypeToJava (string jniType)
	{
		if (jniType.Length == 1) {
			return jniType [0] switch {
				'V' => "void",
				'Z' => "boolean",
				'B' => "byte",
				'C' => "char",
				'S' => "short",
				'I' => "int",
				'J' => "long",
				'F' => "float",
				'D' => "double",
				_ => throw new ArgumentException ($"Unknown JNI primitive type: {jniType}"),
			};
		}

		// Array types: "[I" \u2192 "int[]", "[Ljava/lang/String;" \u2192 "java.lang.String[]"
		if (jniType [0] == '[') {
			return JniTypeToJava (jniType.Substring (1)) + "[]";
		}

		// Object types: "Landroid/os/Bundle;" \u2192 "android.os.Bundle"
		if (jniType [0] == 'L' && jniType [jniType.Length - 1] == ';') {
			return JniNameToJavaName (jniType.Substring (1, jniType.Length - 2));
		}

		throw new ArgumentException ($"Unknown JNI type descriptor: {jniType}");
	}
}
