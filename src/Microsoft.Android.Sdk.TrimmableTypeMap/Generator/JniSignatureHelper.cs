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
	Boolean,  // Z → byte (JNI jboolean is unsigned byte; must be blittable for UCO)
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
	/// Parses JNI parameter type descriptors into JniParameterInfo records.
	/// </summary>
	public static List<JniParameterInfo> ParseParameters (string jniSignature)
	{
		var result = new List<JniParameterInfo> ();
		int i = 1; // skip opening '('
		while (i < jniSignature.Length && jniSignature [i] != ')') {
			int start = i;
			ParseSingleType (jniSignature, ref i);
			result.Add (new JniParameterInfo { JniType = jniSignature.Substring (start, i - start) });
		}
		return result;
	}

	/// <summary>
	/// Extracts the return type descriptor from a JNI method signature.
	/// </summary>
	public static string ParseReturnTypeString (string jniSignature)
	{
		int paren = jniSignature.IndexOf (')');
		if (paren < 0)
			throw new ArgumentException ($"Malformed JNI signature '{jniSignature}': missing ')'");
		return jniSignature.Substring (paren + 1);
	}

	/// <summary>
	/// Parses the return type from a JNI method signature.
	/// </summary>
	public static JniParamKind ParseReturnType (string jniSignature)
	{
		int paren = jniSignature.IndexOf (')');
		if (paren < 0)
			throw new ArgumentException ($"Malformed JNI signature '{jniSignature}': missing ')'");
		int i = paren + 1;
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
			int semi = sig.IndexOf (';', i);
			if (semi < 0)
				throw new ArgumentException ($"Malformed object type in '{sig}' at index {i}: missing ';'");
			i = semi + 1;
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
	/// Encodes a JNI type as its CLR equivalent for [UnmanagedCallersOnly] UCO wrapper signatures.
	/// </summary>
	/// <remarks>
	/// JNI boolean (Z) maps to <c>byte</c> and JNI char (C) maps to <c>ushort</c>,
	/// preserving the JNI ABI with blittable UCO parameter types.
	/// </remarks>
	public static void EncodeClrType (SignatureTypeEncoder encoder, JniParamKind kind)
	{
		switch (kind) {
		case JniParamKind.Boolean: encoder.Byte (); break;   // JNI jboolean is unsigned byte; blittable for UCO
		case JniParamKind.Byte:    encoder.SByte (); break;
		case JniParamKind.Char:    encoder.UInt16 (); break;
		case JniParamKind.Short:   encoder.Int16 (); break;
		case JniParamKind.Int:     encoder.Int32 (); break;
		case JniParamKind.Long:    encoder.Int64 (); break;
		case JniParamKind.Float:   encoder.Single (); break;
		case JniParamKind.Double:  encoder.Double (); break;
		case JniParamKind.Object:  encoder.IntPtr (); break;
		default: throw new ArgumentException ($"Cannot encode JNI param kind {kind} as CLR type");
		}
	}

	/// <summary>
	/// Encodes a JNI type as its CLR equivalent for a <c>n_*</c> callback MemberRef. This is used only
	/// for kinds that are <b>unambiguous</b> across generator versions; the ambiguous kinds — JNI
	/// boolean and char (see <see cref="IsAmbiguousCallbackKind"/>) — are instead emitted from the real
	/// <c>n_*</c> method's captured signature via <see cref="EncodeClrTypeName"/>, because a binding may
	/// declare them as either their managed (<c>bool</c>/<c>char</c>) or blittable (<c>sbyte</c>/<c>ushort</c>)
	/// form depending on which generator compiled it (java-interop #1296).
	/// </summary>
	public static void EncodeClrTypeForCallback (SignatureTypeEncoder encoder, JniParamKind kind)
	{
		switch (kind) {
		// Boolean and Char are ambiguous across generator versions (bool/sbyte, char/ushort) and must
		// be emitted from the real n_* method's captured signature via EncodeClrTypeName — never guessed.
		case JniParamKind.Boolean:
		case JniParamKind.Char:
			throw new ArgumentException ($"JNI {kind} maps to an ambiguous CLR callback type; emit it from the resolved n_* signature instead of the JNI descriptor.");
		case JniParamKind.Byte:    encoder.SByte (); break;
		case JniParamKind.Short:   encoder.Int16 (); break;
		case JniParamKind.Int:     encoder.Int32 (); break;
		case JniParamKind.Long:    encoder.Int64 (); break;
		case JniParamKind.Float:   encoder.Single (); break;
		case JniParamKind.Double:  encoder.Double (); break;
		case JniParamKind.Object:  encoder.IntPtr (); break;
		default: throw new ArgumentException ($"Cannot encode JNI param kind {kind} as CLR type");
		}
	}

	/// <summary>
	/// True when a JNI kind maps to a CLR type that the MCW <c>n_*</c> callback may declare as either
	/// its managed form or a blittable form depending on the generator version that compiled the
	/// binding: JNI boolean is <c>bool</c> (pre-#1296) or <c>sbyte</c> (post-#1296), and JNI char is
	/// <c>char</c> or <c>ushort</c>. For these, the callback MemberRef must be built from the real
	/// <c>n_*</c> method's captured signature rather than guessed from the JNI descriptor.
	/// </summary>
	public static bool IsAmbiguousCallbackKind (JniParamKind kind)
		=> kind == JniParamKind.Boolean || kind == JniParamKind.Char;

	/// <summary>
	/// True when a JNI method signature contains a boolean or char parameter/return — the kinds whose
	/// <c>n_*</c> callback CLR type is ambiguous across generator versions and must therefore be
	/// captured from the real method's metadata (see <see cref="IsAmbiguousCallbackKind"/>).
	/// </summary>
	public static bool HasAmbiguousCallbackType (string jniSignature)
	{
		if (IsAmbiguousCallbackKind (ParseReturnType (jniSignature))) {
			return true;
		}
		foreach (var kind in ParseParameterTypes (jniSignature)) {
			if (IsAmbiguousCallbackKind (kind)) {
				return true;
			}
		}
		return false;
	}

	/// <summary>
	/// Encodes a captured CLR type-name string (e.g. "System.Boolean", "System.SByte", "System.IntPtr")
	/// onto a signature, used to emit a callback MemberRef that mirrors the real <c>n_*</c> method.
	/// </summary>
	public static void EncodeClrTypeName (SignatureTypeEncoder encoder, string clrTypeName)
	{
		switch (clrTypeName) {
		case "System.Boolean": encoder.Boolean (); break;
		case "System.SByte":   encoder.SByte (); break;
		case "System.Byte":    encoder.Byte (); break;
		case "System.Char":    encoder.Char (); break;
		case "System.UInt16":  encoder.UInt16 (); break;
		case "System.Int16":   encoder.Int16 (); break;
		case "System.Int32":   encoder.Int32 (); break;
		case "System.UInt32":  encoder.UInt32 (); break;
		case "System.Int64":   encoder.Int64 (); break;
		case "System.UInt64":  encoder.UInt64 (); break;
		case "System.Single":  encoder.Single (); break;
		case "System.Double":  encoder.Double (); break;
		case "System.IntPtr":  encoder.IntPtr (); break;
		default: throw new ArgumentException ($"Cannot encode CLR type '{clrTypeName}' in a native callback MemberRef");
		}
	}

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
	/// JNI uses '/' for packages and '$' for inner classes.
	/// Java source uses '.' for both.
	/// e.g., "android/app/Activity" → "android.app.Activity"
	/// e.g., "android/drm/DrmManagerClient$OnEventListener" → "android.drm.DrmManagerClient.OnEventListener"
	/// </summary>
	internal static string JniNameToJavaName (string jniName)
	{
		return jniName.Replace ('/', '.').Replace ('$', '.');
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
