using System.Collections.Generic;
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
