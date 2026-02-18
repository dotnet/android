using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>JNI primitive type kinds used for mapping JNI signatures → CLR types.</summary>
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

/// <summary>Helpers for parsing JNI method signatures.</summary>
static class JniSignatureHelper
{
	/// <summary>Parses the parameter types from a JNI method signature like "(Landroid/os/Bundle;)V".</summary>
	public static List<JniParamKind> ParseParameterTypes (string jniSignature)
	{
		var result = new List<JniParamKind> ();
		int i = 1; // skip opening '('
		while (i < jniSignature.Length && jniSignature [i] != ')') {
			result.Add (ParseSingleType (jniSignature, ref i));
		}
		return result;
	}

	/// <summary>Parses the raw JNI type descriptor strings from a JNI method signature.</summary>
	public static List<string> ParseParameterTypeStrings (string jniSignature)
	{
		var result = new List<string> ();
		int i = 1; // skip opening '('
		while (i < jniSignature.Length && jniSignature [i] != ')') {
			int start = i;
			SkipSingleType (jniSignature, ref i);
			result.Add (jniSignature.Substring (start, i - start));
		}
		return result;
	}

	/// <summary>Extracts the return type descriptor from a JNI method signature.</summary>
	public static string ParseReturnTypeString (string jniSignature)
	{
		int i = jniSignature.IndexOf (')') + 1;
		return jniSignature.Substring (i);
	}

	/// <summary>Parses the return type from a JNI method signature.</summary>
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

	static void SkipSingleType (string sig, ref int i)
	{
		switch (sig [i]) {
		case 'V': case 'Z': case 'B': case 'C': case 'S':
		case 'I': case 'J': case 'F': case 'D':
			i++;
			break;
		case 'L':
			i = sig.IndexOf (';', i) + 1;
			break;
		case '[':
			i++;
			SkipSingleType (sig, ref i);
			break;
		default:
			throw new ArgumentException ($"Unknown JNI type character '{sig [i]}' in '{sig}' at index {i}");
		}
	}

	/// <summary>Encodes the CLR type for a JNI parameter kind into a signature type encoder.</summary>
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
}
