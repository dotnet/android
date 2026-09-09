using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class JavaNameValidatorTests
{
	public static IEnumerable<object []> ReservedIdentifiers {
		get {
			string [] identifiers = [
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
			];

			foreach (var identifier in identifiers) {
				yield return [identifier];
			}
		}
	}

	public static IEnumerable<object []> RestrictedTypeIdentifiers {
		get {
			yield return ["permits"];
			yield return ["record"];
			yield return ["sealed"];
			yield return ["var"];
			yield return ["yield"];
		}
	}

	[Theory]
	[MemberData (nameof (ReservedIdentifiers))]
	public void TryGetInvalidPackageSegment_ReservedIdentifier_ReturnsTrue (string identifier)
	{
		Assert.True (JavaNameValidator.TryGetInvalidPackageSegment ($"com.{identifier}.example", '.', out var actual));
		Assert.Equal (identifier, actual);
	}

	[Theory]
	[MemberData (nameof (ReservedIdentifiers))]
	public void TryGetInvalidJniNameSegment_ReservedPackageIdentifier_ReturnsTrue (string identifier)
	{
		Assert.True (JavaNameValidator.TryGetInvalidJniNameSegment ($"com/{identifier}/Example", out var actual));
		Assert.Equal (identifier, actual);
	}

	[Theory]
	[MemberData (nameof (ReservedIdentifiers))]
	public void TryGetInvalidJniNameSegment_ReservedTypeIdentifier_ReturnsTrue (string identifier)
	{
		Assert.True (JavaNameValidator.TryGetInvalidJniNameSegment ($"com/example/{identifier}", out var actual));
		Assert.Equal (identifier, actual);
	}

	[Theory]
	[InlineData ("com/example/Outer$for", "for")]
	[InlineData ("com/example/Outer$record", "record")]
	[InlineData ("com/example/for$Nested", "for")]
	public void TryGetInvalidJniSourceTypeSegment_ReservedNestedTypeIdentifier_ReturnsTrue (string jniName, string expected)
	{
		Assert.False (JavaNameValidator.TryGetInvalidJniNameSegment (jniName, out _));
		Assert.True (JavaNameValidator.TryGetInvalidJniSourceTypeSegment (jniName, out var actual));
		Assert.Equal (expected, actual);
	}

	[Theory]
	[MemberData (nameof (RestrictedTypeIdentifiers))]
	public void RestrictedTypeIdentifier_IsValidInPackageButInvalidAsType (string identifier)
	{
		Assert.False (JavaNameValidator.TryGetInvalidPackageSegment ($"com.example.{identifier}", '.', out var packageIdentifier));
		Assert.Equal ("", packageIdentifier);
		Assert.False (JavaNameValidator.TryGetInvalidJniNameSegment ($"com/{identifier}/Example", out var jniPackageIdentifier));
		Assert.Equal ("", jniPackageIdentifier);
		Assert.True (JavaNameValidator.TryGetInvalidJniNameSegment ($"com/example/{identifier}", out var typeIdentifier));
		Assert.Equal (identifier, typeIdentifier);
	}

	[Theory]
	[InlineData ("module")]
	[InlineData ("open")]
	[InlineData ("requires")]
	[InlineData ("exports")]
	[InlineData ("opens")]
	[InlineData ("to")]
	[InlineData ("uses")]
	[InlineData ("provides")]
	[InlineData ("with")]
	[InlineData ("transitive")]
	public void ModuleContextualKeyword_IsValidIdentifier (string identifier)
	{
		Assert.False (JavaNameValidator.TryGetInvalidPackageSegment ($"com.example.{identifier}", '.', out _));
		Assert.False (JavaNameValidator.TryGetInvalidJniNameSegment ($"com/{identifier}/Example", out _));
		Assert.False (JavaNameValidator.TryGetInvalidJniNameSegment ($"com/example/{identifier}", out _));
	}

	[Fact]
	public void ValidNames_ReturnFalseAndEmptyIdentifier ()
	{
		Assert.False (JavaNameValidator.TryGetInvalidPackageSegment ("com.example.app", '.', out var packageIdentifier));
		Assert.Equal ("", packageIdentifier);
		Assert.False (JavaNameValidator.TryGetInvalidJniNameSegment ("com/example/MainActivity", out var jniIdentifier));
		Assert.Equal ("", jniIdentifier);
		Assert.False (JavaNameValidator.TryGetInvalidJniNameSegment ("com/example/Outer$Inner", out var nestedIdentifier));
		Assert.Equal ("", nestedIdentifier);
		Assert.False (JavaNameValidator.TryGetInvalidJniSourceTypeSegment ("com/example/Outer$Inner", out nestedIdentifier));
		Assert.Equal ("", nestedIdentifier);
	}

	[Theory]
	[InlineData ("com/\u00e9xample/\u0394elta")]
	[InlineData ("com/example/\u00a2Peer")]
	[InlineData ("com/example/\u203fPeer")]
	[InlineData ("com/example/A\u0660")]
	public void UnicodeIdentifiers_AreValid (string jniName)
	{
		Assert.False (JavaNameValidator.TryGetInvalidJniNameSegment (jniName, out var invalidIdentifier));
		Assert.Equal ("", invalidIdentifier);
	}

	[Theory]
	[InlineData ("com/example/\u00a2Outer$Inner")]
	[InlineData ("com/example/\u203fOuter$Inner")]
	public void JavaTypeOnlyStart_OnNestedType_IsValid (string jniName)
	{
		Assert.False (JavaNameValidator.TryGetInvalidJniSourceTypeSegment (jniName, out _));
		Assert.False (JavaNameValidator.TryGetInvalidJniTypeSegment ($"L{jniName};", out _, out _));
		Assert.False (
			JavaNameValidator.TryGetInvalidJavaSourceTypeSegment (
				JniSignatureHelper.JniNameToJavaName (jniName),
				out _
			)
		);
	}

	[Theory]
	[InlineData ('\u00a2', true, true)]
	[InlineData ('\u203f', true, true)]
	[InlineData ('\u0cf3', false, true)]
	[InlineData ('\u0301', false, true)]
	[InlineData ('\u0660', false, true)]
	[InlineData ('\u1c89', false, false)]
	public void FrozenJdk21IdentifierData_MatchesCharacterClassification (
		char value,
		bool isStart,
		bool isPart)
	{
		Assert.Equal (isStart, JavaIdentifierData.IsIdentifierStart (value));
		Assert.Equal (isPart, JavaIdentifierData.IsIdentifierPart (value));
	}

	[Theory]
	[InlineData (0x10400, true, true)]
	[InlineData (0x10428, true, true)]
	[InlineData (0x1b132, true, true)]
	public void FrozenJdk21SupplementaryIdentifierData_MatchesCharacterClassification (
		int value,
		bool isStart,
		bool isPart)
	{
		Assert.Equal (isStart, JavaIdentifierData.IsIdentifierStart (value));
		Assert.Equal (isPart, JavaIdentifierData.IsIdentifierPart (value));
	}

	[Fact]
	public void FrozenJdk21IdentifierData_HasExpectedClassificationHash ()
	{
		const int maxCodePoint = 0x10ffff;
		var classification = new byte [maxCodePoint + 1];
		for (int value = 0; value <= maxCodePoint; value++) {
			if (JavaIdentifierData.IsIdentifierStart (value)) {
				classification [value] |= 1;
			}
			if (JavaIdentifierData.IsIdentifierPart (value)) {
				classification [value] |= 2;
			}
		}

		Assert.Equal (
			"81b63b25dd80b36fcd964822c76bc18fe9c04319876d26b4ba3983f7d7090319",
			Convert.ToHexString (SHA256.HashData (classification)).ToLowerInvariant ()
		);
	}

	[Theory]
	[InlineData ("com/example/1Peer", "1Peer")]
	[InlineData ("com/example/\u0301Peer", "\u0301Peer")]
	[InlineData ("com/1example/Peer", "1example")]
	[InlineData ("com/\u0301example/Peer", "\u0301example")]
	[InlineData ("com/e\u0301xample/Cafe\u0301", "e\u0301xample")]
	[InlineData ("com/example/A\u0cf3", "A\u0cf3")]
	[InlineData ("com/example/\U00010428Peer\U00010400", "\U00010428Peer\U00010400")]
	[InlineData ("com/example/\u1c89Peer", "\u1c89Peer")]
	[InlineData ("com/example/\u212bPeer", "\u212bPeer")]
	public void InvalidOrUnsupportedIdentifier_ReturnsSegment (string jniName, string expected)
	{
		Assert.True (JavaNameValidator.TryGetInvalidJniNameSegment (jniName, out var invalidIdentifier));
		Assert.Equal (expected, invalidIdentifier);
	}

	[Theory]
	[InlineData ("com.\u00a2pkg.example", "\u00a2pkg")]
	[InlineData ("com.\u203fpkg.example", "\u203fpkg")]
	[InlineData ("com.A\u0660.example", "A\u0660")]
	[InlineData ("com.\U00010428pkg.example", "\U00010428pkg")]
	public void JavaTypeOnlyIdentifiers_AreInvalidPackageSegments (string packageName, string expected)
	{
		Assert.True (JavaNameValidator.TryGetInvalidPackageSegment (packageName, '.', out var invalidIdentifier));
		Assert.Equal (expected, invalidIdentifier);
		Assert.True (
			JavaNameValidator.TryGetInvalidJniNameSegment (
				packageName.Replace ('.', '/') + "/Peer",
				out invalidIdentifier
			)
		);
		Assert.Equal (expected, invalidIdentifier);
	}

	[Fact]
	public void ComposedAndDecomposedIdentifiers_AreNotNormalized ()
	{
		const string composed = "com/\u00e9xample/Peer";
		const string decomposed = "com/e\u0301xample/Peer";
		const string canonicalComposed = "com/example/\u00c5Peer";
		const string canonicalEquivalent = "com/example/\u212bPeer";

		Assert.False (JavaNameValidator.TryGetInvalidJniNameSegment (composed, out _));
		Assert.True (JavaNameValidator.TryGetInvalidJniNameSegment (decomposed, out _));
		Assert.NotEqual (composed, decomposed);
		Assert.False (JavaNameValidator.TryGetInvalidJniNameSegment (canonicalComposed, out _));
		Assert.True (JavaNameValidator.TryGetInvalidJniNameSegment (canonicalEquivalent, out _));
	}

	[Theory]
	[InlineData ("Lcom/example/Outer$for;", "com/example/Outer$for", "for")]
	[InlineData ("[[Lcom/example/Outer$record;", "com/example/Outer$record", "record")]
	public void TryGetInvalidJniTypeSegment_ReservedTypeIdentifier_ReturnsTrue (string jniType, string expectedTypeName, string expectedIdentifier)
	{
		Assert.True (JavaNameValidator.TryGetInvalidJniTypeSegment (jniType, out var typeName, out var invalidIdentifier));
		Assert.Equal (expectedTypeName, typeName);
		Assert.Equal (expectedIdentifier, invalidIdentifier);
	}

	[Theory]
	[InlineData ("com.example.Outer.for", "for")]
	[InlineData ("Outer$record", "record")]
	[InlineData ("com.example.record", "record")]
	[InlineData ("com.record.Example", null)]
	[InlineData ("int[]", null)]
	public void TryGetInvalidJavaSourceTypeSegment_ValidatesEmittedTypeName (string javaType, string? expectedIdentifier)
	{
		bool invalid = JavaNameValidator.TryGetInvalidJavaSourceTypeSegment (javaType, out var invalidIdentifier);
		Assert.Equal (expectedIdentifier is not null, invalid);
		Assert.Equal (expectedIdentifier ?? "", invalidIdentifier);
	}
}
