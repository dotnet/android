using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

/// <summary>
/// Helpers for parsing JNI method signatures.
/// </summary>
static class JniSignatureHelper
{
	/// <summary>
	/// Parses the raw JNI type descriptor strings from a JNI method signature.
	/// </summary>
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

	/// <summary>
	/// Extracts the return type descriptor from a JNI method signature.
	/// </summary>
	public static string ParseReturnTypeString (string jniSignature)
	{
		int i = jniSignature.IndexOf (')') + 1;
		return jniSignature.Substring (i);
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
}
