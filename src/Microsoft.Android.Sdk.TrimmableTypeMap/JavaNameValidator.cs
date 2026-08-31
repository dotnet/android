using System;
using System.Collections.Generic;
using System.Text;

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
		!IsSupportedIdentifier (identifier, isPackageSegment: false) ||
		JavaKeywords.Contains (identifier) ||
		isTypeName && RestrictedTypeIdentifiers.Contains (identifier);

	static bool IsInvalidPackageIdentifier (string identifier) =>
		!IsSupportedIdentifier (identifier, isPackageSegment: true) ||
		JavaKeywords.Contains (identifier);

	// JLS 3.8 permits combining, format, and supplementary characters, but generated JCWs also need
	// stable source paths and class names throughout javac, AAPT, DEX, and the Android runtime.
	static bool IsSupportedIdentifier (string identifier, bool isPackageSegment)
	{
		if (identifier.Length == 0) {
			return false;
		}

		for (int i = 0; i < identifier.Length; i++) {
			char value = identifier [i];
			if (char.IsSurrogate (value)) {
				// javac accepts supplementary letters, but Android's DEX pipeline omits those classes.
				return false;
			}
			bool valid = i == 0
				? IsIdentifierStart (value, isPackageSegment)
				: IsIdentifierPart (value, isPackageSegment);
			if (!valid) {
				return false;
			}
		}

		return identifier.IsNormalized (NormalizationForm.FormC);
	}

	static bool IsIdentifierStart (char value, bool isPackageSegment) =>
		isPackageSegment
			? JavaIdentifierData.IsPackageIdentifierStart (value)
			: JavaIdentifierData.IsIdentifierStart (value);

	static bool IsIdentifierPart (char value, bool isPackageSegment) =>
		isPackageSegment
			? JavaIdentifierData.IsPackageIdentifierPart (value)
			: JavaIdentifierData.IsSupportedIdentifierPart (value);

	internal static bool TryGetInvalidPackageSegment (string packageName, char separator, out string invalidSegment)
	{
		foreach (var segment in packageName.Split (separator)) {
			if (IsInvalidPackageIdentifier (segment)) {
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
			if (IsInvalidPackageIdentifier (segments [i])) {
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

	internal static bool TryGetInvalidJniManifestNameSegment (string jniName, out string invalidSegment)
	{
		var segments = jniName.Split ('/');
		for (int i = 0; i < segments.Length; i++) {
			bool isTypeName = i == segments.Length - 1;
			if (IsInvalidPackageIdentifier (segments [i]) ||
					isTypeName && RestrictedTypeIdentifiers.Contains (segments [i])) {
				invalidSegment = segments [i];
				return true;
			}
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

	internal static bool TryGetInvalidJniTypeSegment (string jniType, out string typeName, out string invalidSegment)
	{
		int typeStart = 0;
		while (typeStart < jniType.Length && jniType [typeStart] == '[') {
			typeStart++;
		}

		if (typeStart < jniType.Length - 1 && jniType [typeStart] == 'L' && jniType [jniType.Length - 1] == ';') {
			typeName = jniType.Substring (typeStart + 1, jniType.Length - typeStart - 2);
			return TryGetInvalidJniSourceTypeSegment (typeName, out invalidSegment);
		}

		typeName = "";
		invalidSegment = "";
		return false;
	}

	internal static bool TryGetInvalidJavaSourceTypeSegment (string javaType, out string invalidSegment)
	{
		string typeName = javaType;
		while (typeName.EndsWith ("[]", StringComparison.Ordinal)) {
			typeName = typeName.Substring (0, typeName.Length - 2);
		}
		if (typeName is "boolean" or "byte" or "char" or "short" or "int" or "long" or "float" or "double" or "void") {
			invalidSegment = "";
			return false;
		}

		var segments = typeName.Split ('.');
		for (int i = 0; i < segments.Length; i++) {
			var nestedSegments = segments [i].Split ('$');
			for (int nestedIndex = 0; nestedIndex < nestedSegments.Length; nestedIndex++) {
				bool isTypeName = i == segments.Length - 1 || nestedIndex > 0;
				if (IsInvalidIdentifier (nestedSegments [nestedIndex], isTypeName)) {
					invalidSegment = nestedSegments [nestedIndex];
					return true;
				}
			}
		}

		invalidSegment = "";
		return false;
	}

	internal static bool TryGetInvalidJavaManifestTypeSegment (string javaType, out string invalidSegment)
	{
		var segments = javaType.Split ('.');
		for (int i = 0; i < segments.Length; i++) {
			bool isTypeName = i == segments.Length - 1;
			if (IsInvalidPackageIdentifier (segments [i]) ||
					isTypeName && RestrictedTypeIdentifiers.Contains (segments [i])) {
				invalidSegment = segments [i];
				return true;
			}
		}

		invalidSegment = "";
		return false;
	}
}
