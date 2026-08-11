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
		for (int i = 0; i < segments.Length - 1; i++) {
			if (JavaKeywords.Contains (segments [i])) {
				invalidSegment = segments [i];
				return true;
			}
		}

		string typeName = segments [segments.Length - 1];
		if (IsInvalidIdentifier (typeName, isTypeName: true)) {
			invalidSegment = typeName;
			return true;
		}

		invalidSegment = "";
		return false;
	}

	internal static bool TryGetInvalidJniSourceTypeSegment (string jniName, out string invalidSegment)
	{
		if (TryGetInvalidJniNameSegment (jniName, out invalidSegment)) {
			return true;
		}

		// '$' becomes '.' when a JNI binary name is emitted as a Java source type reference.
		string typeName = jniName.Substring (jniName.LastIndexOf ('/') + 1);
		foreach (var segment in typeName.Split ('$')) {
			if (IsInvalidIdentifier (segment, isTypeName: true)) {
				invalidSegment = segment;
				return true;
			}
		}

		return false;
	}
}
