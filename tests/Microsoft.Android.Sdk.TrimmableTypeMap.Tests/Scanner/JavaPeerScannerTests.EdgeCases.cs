using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
	[Fact]
	public void Scan_GenericTypes_ResolveViaTypeSpecification ()
	{
		Assert.Equal ("my/app/GenericBase",
			FindFixtureByJavaName ("my/app/ConcreteFromGeneric").BaseJavaName);
		Assert.Contains ("my/app/IGenericCallback",
			FindFixtureByJavaName ("my/app/GenericCallbackImpl").ImplementedInterfaceJavaNames);
	}

	[Fact]
	public void Scan_ComponentOnlyBase_BothBaseAndDerivedDiscovered ()
	{
		var baseType = FindFixtureByJavaName ("my/app/BaseActivityNoRegister");
		Assert.True (baseType.IsUnconditional);
		Assert.Equal ("android/app/Activity", baseType.BaseJavaName);

		var derived = FindFixtureByManagedName ("MyApp.DerivedFromComponentBase");
		Assert.StartsWith ("crc64", derived.JavaName);
	}

	[Theory]
	[InlineData ("MyApp.RegisteredParent+UnregisteredChild", "my/app/RegisteredParent_UnregisteredChild")]
	[InlineData ("MyApp.DeepOuter+Middle+DeepInner", "my/app/DeepOuter_Middle_DeepInner")]
	public void Scan_UnregisteredNestedType_UsesParentJniPrefix (string managedName, string expectedJavaName)
	{
		Assert.Equal (expectedJavaName, FindFixtureByManagedName (managedName).JavaName);
	}

	[Fact]
	public void Scan_EmptyNamespace_Handled ()
	{
		Assert.Equal ("GlobalType", FindFixtureByJavaName ("my/app/GlobalType").ManagedTypeName);
	}

	[Theory]
	[InlineData ("MyApp.PlainActivitySubclass")]
	[InlineData ("MyApp.UnnamedActivity")]
	[InlineData ("MyApp.UnregisteredClickListener")]
	[InlineData ("MyApp.UnregisteredExporter")]
	public void Scan_UnregisteredType_DiscoveredWithCrc64Name (string managedName)
	{
		Assert.StartsWith ("crc64", FindFixtureByManagedName (managedName).JavaName);
	}

}
