using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Android.Sdk.TrimmableTypeMap;

public static class JavaNameValidator
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

	internal static bool IsInvalidIdentifier (ReadOnlySpan<char> identifier, bool isTypeName) =>
		!IsSupportedIdentifier (identifier, isPackageSegment: false) ||
		JavaKeywords.GetAlternateLookup<ReadOnlySpan<char>> ().Contains (identifier) ||
		isTypeName && RestrictedTypeIdentifiers.GetAlternateLookup<ReadOnlySpan<char>> ().Contains (identifier);

	static bool IsInvalidPackageIdentifier (ReadOnlySpan<char> identifier) =>
		!IsSupportedIdentifier (identifier, isPackageSegment: true) ||
		JavaKeywords.GetAlternateLookup<ReadOnlySpan<char>> ().Contains (identifier);

	// JLS 3.8 permits combining, format, and supplementary characters, but generated JCWs also need
	// stable source paths and class names throughout javac, AAPT, DEX, and the Android runtime.
	static bool IsSupportedIdentifier (ReadOnlySpan<char> identifier, bool isPackageSegment)
	{
		if (identifier.Length == 0) {
			return false;
		}

		int codePointIndex = 0;
		for (int i = 0; i < identifier.Length; codePointIndex++) {
			char first = identifier [i];
			int value;
			if (char.IsHighSurrogate (first)) {
				if (i + 1 >= identifier.Length || !char.IsLowSurrogate (identifier [i + 1])) {
					return false;
				}
				// JDK 21 and DEX preserve supplementary identifier characters, but Android's
				// class loader cannot resolve classes whose simple name contains them.
				return false;
			} else if (char.IsLowSurrogate (first)) {
				return false;
			} else {
				value = first;
				i++;
			}
			bool valid = codePointIndex == 0
				? IsIdentifierStart (value, isPackageSegment)
				: IsIdentifierPart (value, isPackageSegment);
			if (!valid) {
				return false;
			}
		}

		return identifier.IsNormalized (NormalizationForm.FormC);
	}

	static bool IsIdentifierStart (int value, bool isPackageSegment) =>
		isPackageSegment
			? value <= char.MaxValue && JavaIdentifierData.IsPackageIdentifierStart ((char) value)
			: JavaIdentifierData.IsIdentifierStart (value);

	static bool IsIdentifierPart (int value, bool isPackageSegment) =>
		isPackageSegment
			? value <= char.MaxValue && JavaIdentifierData.IsPackageIdentifierPart ((char) value)
			: JavaIdentifierData.IsSupportedIdentifierPart (value);

	public static bool TryGetInvalidPackageSegment (ReadOnlySpan<char> packageName, char separator, out ReadOnlySpan<char> invalidSegment)
	{
		foreach (var range in packageName.Split (separator)) {
			var segment = packageName [range];
			if (IsInvalidPackageIdentifier (segment)) {
				invalidSegment = segment;
				return true;
			}
		}

		invalidSegment = default;
		return false;
	}

	internal static bool TryGetInvalidJniNameSegment (ReadOnlySpan<char> jniName, out ReadOnlySpan<char> invalidSegment)
	{
		int lastSlash = jniName.LastIndexOf ('/');
		if (lastSlash >= 0 && TryGetInvalidPackageSegment (jniName [..lastSlash], '/', out invalidSegment)) {
			return true;
		}

		var typeName = jniName [(lastSlash + 1)..];
		if (IsInvalidIdentifier (typeName, isTypeName: true)) {
			invalidSegment = typeName;
			return true;
		}

		invalidSegment = default;
		return false;
	}

	internal static bool TryGetInvalidJniManifestNameSegment (ReadOnlySpan<char> jniName, out ReadOnlySpan<char> invalidSegment)
	{
		foreach (var range in jniName.Split ('/')) {
			var segment = jniName [range];
			bool isTypeName = range.End.Value == jniName.Length;
			if (IsInvalidPackageIdentifier (segment) ||
					isTypeName && RestrictedTypeIdentifiers.GetAlternateLookup<ReadOnlySpan<char>> ().Contains (segment)) {
				invalidSegment = segment;
				return true;
			}
		}

		invalidSegment = default;
		return false;
	}

	internal static bool TryGetInvalidJniSourceTypeSegment (ReadOnlySpan<char> jniName, out ReadOnlySpan<char> invalidSegment)
	{
		if (TryGetInvalidJniNameSegment (jniName, out invalidSegment)) {
			return true;
		}

		// '$' becomes '.' when a JNI binary name is emitted as a Java source type reference.
		var typeName = jniName [(jniName.LastIndexOf ('/') + 1)..];
		foreach (var range in typeName.Split ('$')) {
			var segment = typeName [range];
			if (IsInvalidIdentifier (segment, isTypeName: true)) {
				invalidSegment = segment;
				return true;
			}
		}

		return false;
	}

	internal static bool TryGetInvalidJniTypeSegment (ReadOnlySpan<char> jniType, out ReadOnlySpan<char> typeName, out ReadOnlySpan<char> invalidSegment)
	{
		int typeStart = 0;
		while (typeStart < jniType.Length && jniType [typeStart] == '[') {
			typeStart++;
		}

		if (typeStart < jniType.Length - 1 && jniType [typeStart] == 'L' && jniType [jniType.Length - 1] == ';') {
			typeName = jniType [(typeStart + 1)..^1];
			return TryGetInvalidJniSourceTypeSegment (typeName, out invalidSegment);
		}

		typeName = default;
		invalidSegment = default;
		return false;
	}

	internal static bool TryGetInvalidJavaSourceTypeSegment (ReadOnlySpan<char> javaType, out ReadOnlySpan<char> invalidSegment)
	{
		var typeName = javaType;
		while (typeName.EndsWith ("[]", StringComparison.Ordinal)) {
			typeName = typeName [..^2];
		}
		if (typeName is "boolean" or "byte" or "char" or "short" or "int" or "long" or "float" or "double" or "void") {
			invalidSegment = default;
			return false;
		}

		foreach (var range in typeName.Split ('.')) {
			var segment = typeName [range];
			foreach (var nestedRange in segment.Split ('$')) {
				var nestedSegment = segment [nestedRange];
				bool isTypeName = range.End.Value == typeName.Length || nestedRange.Start.Value > 0;
				if (IsInvalidIdentifier (nestedSegment, isTypeName)) {
					invalidSegment = nestedSegment;
					return true;
				}
			}
		}

		invalidSegment = default;
		return false;
	}

	internal static bool TryGetInvalidJavaManifestTypeSegment (ReadOnlySpan<char> javaType, out ReadOnlySpan<char> invalidSegment)
	{
		foreach (var range in javaType.Split ('.')) {
			var segment = javaType [range];
			bool isTypeName = range.End.Value == javaType.Length;
			if (IsInvalidPackageIdentifier (segment) ||
					isTypeName && RestrictedTypeIdentifiers.GetAlternateLookup<ReadOnlySpan<char>> ().Contains (segment)) {
				invalidSegment = segment;
				return true;
			}
		}

		invalidSegment = default;
		return false;
	}
}
