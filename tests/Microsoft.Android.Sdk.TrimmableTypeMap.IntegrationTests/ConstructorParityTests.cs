using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

public partial class ScannerComparisonTests
{
	[Theory]
	[InlineData ("UserApp.SignedUnsignedConstructorCollision", "(I)V")]
	[InlineData ("UserApp.AliasedTypeConstructorCollision", "(Lcom/example/userapp/Alias;)V")]
	public void LegacyConstructorSignatureCollision_KeepsFirstSignature (string managedTypeName, string signature)
	{
		var fixturePath = UserTypesFixturePath;
		Assert.NotNull (fixturePath);
		var constructors = ScannerRunner.RunLegacyConstructors (fixturePath, managedTypeName);
		Assert.Single (constructors, c => c.JniSignature == signature);
	}

	[Theory]
	[InlineData ("UserApp.GenericParameterConstructor`1")]
	[InlineData ("UserApp.GenericInstantiationConstructor")]
	[InlineData ("UserApp.FunctionPointerConstructor")]
	[InlineData ("UserApp.FunctionPointerArrayConstructor")]
	public void LegacyUnrepresentableConstructor_IsSkipped (string managedTypeName)
	{
		var fixturePath = UserTypesFixturePath;
		Assert.NotNull (fixturePath);
		var constructors = ScannerRunner.RunLegacyConstructors (fixturePath, managedTypeName);
		var constructor = Assert.Single (constructors);
		Assert.Equal ("()V", constructor.JniSignature);
	}

	[Theory]
	[InlineData ("UserApp.ByRefConstructor")]
	[InlineData ("UserApp.PointerConstructor")]
	public void LegacyElementMappedConstructor_UsesMeasuredJniSignatures (string managedTypeName)
	{
		var fixturePath = UserTypesFixturePath;
		Assert.NotNull (fixturePath);
		var signatures = ScannerRunner.RunLegacyConstructors (fixturePath, managedTypeName)
			.Select (constructor => constructor.JniSignature)
			.ToList ();
		Assert.Equal (["()V", "(I)V"], signatures);
	}

	[Theory]
	[InlineData ("UserApp.RectangularArrayConstructor", "([Ljava/lang/String;)V")]
	[InlineData ("UserApp.NestedRectangularArrayConstructor", "([Ljava/lang/String;)V")]
	[InlineData ("UserApp.PointerArrayConstructor", "([I)V")]
	[InlineData ("UserApp.JaggedArrayConstructor", "([Ljava/lang/String;)V")]
	public void LegacyArrayConstructor_UsesMeasuredJniSignature (string managedTypeName, string signature)
	{
		var fixturePath = UserTypesFixturePath;
		Assert.NotNull (fixturePath);
		var constructors = ScannerRunner.RunLegacyConstructors (fixturePath, managedTypeName);
		Assert.Single (constructors, c => c.JniSignature == signature);
	}

	[Fact]
	public void LegacyMissingBaseConstructor_IsSkipped ()
	{
		var fixturePath = UserTypesFixturePath;
		Assert.NotNull (fixturePath);
		var constructors = ScannerRunner.RunLegacyConstructors (fixturePath, "UserApp.MissingBaseConstructor");
		Assert.DoesNotContain (constructors, c => c.JniSignature == "(Ljava/lang/String;)V");
	}

	[Fact]
	public void LegacyInvalidSuperArgumentsString_IsEmittedVerbatim ()
	{
		var fixturePath = UserTypesFixturePath;
		Assert.NotNull (fixturePath);
		var constructor = Assert.Single (
			ScannerRunner.RunLegacyConstructors (fixturePath, "UserApp.InvalidSuperArgumentsConstructor"),
			c => c.JniSignature == "(Ljava/lang/String;)V");
		Assert.Equal ("p1", constructor.SuperCall);
	}
}
