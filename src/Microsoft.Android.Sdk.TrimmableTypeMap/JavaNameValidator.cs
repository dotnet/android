using System;
using System.Collections.Generic;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

internal static class JavaNameValidator
{
	// Java SE 21 reserved keywords and literals:
	// https://docs.oracle.com/javase/specs/jls/se21/html/jls-3.html#jls-3.9
	static readonly HashSet<string> JavaKeywords = new (StringComparer.Ordinal) {
		"_",
		"abstract", "assert", "boolean", "break", "byte",
		"case", "catch", "char", "class", "const", "continue",
		"default", "do", "double",
		"else", "enum", "extends",
		"false", "final", "finally", "float", "for",
		"goto",
		"if", "implements", "import", "instanceof", "int", "interface",
		"long",
		"native", "new", "null",
		"package", "private", "protected", "public",
		"return",
		"short", "static", "strictfp", "super", "switch", "synchronized",
		"this", "throw", "throws", "transient", "true", "try",
		"void", "volatile",
		"while",
	};

	// TypeIdentifier additionally excludes these contextual keywords:
	// https://docs.oracle.com/javase/specs/jls/se21/html/jls-3.html#jls-TypeIdentifier
	static readonly HashSet<string> RestrictedTypeIdentifiers = new (StringComparer.Ordinal) {
		"permits", "record", "sealed", "var", "yield",
	};

	internal static bool IsInvalidIdentifier (string identifier, bool isTypeName) =>
		JavaKeywords.Contains (identifier) || isTypeName && RestrictedTypeIdentifiers.Contains (identifier);

	internal static bool TryGetInvalidPackageSegment (string packageName, char separator, out string invalidSegment)
	{
		foreach (var segment in packageName.Split (separator)) {
			if (JavaKeywords.Contains (segment)) {
				invalidSegment = segment;
				return true;
			}
		}

		invalidSegment = "";
		return false;
	}

	internal static bool TryGetInvalidJniNameSegment (string jniName, out string invalidSegment)
	{
		var segments = jniName.Split ('/');
		for (int i = 0; i < segments.Length; i++) {
			string segment = segments [i];
			if (IsInvalidIdentifier (segment, i == segments.Length - 1)) {
				invalidSegment = segment;
				return true;
			}
		}

		invalidSegment = "";
		return false;
	}
}
