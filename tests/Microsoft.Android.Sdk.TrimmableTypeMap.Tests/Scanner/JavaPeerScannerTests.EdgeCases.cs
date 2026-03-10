using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
	[Fact]
	public void Scan_GenericTypes_ResolveViaTypeSpecification ()
	{
		var peers = ScanFixtures ();
		Assert.Equal ("my/app/GenericBase",
			FindByJavaName (peers, "my/app/ConcreteFromGeneric").BaseJavaName);
		Assert.Contains ("my/app/IGenericCallback",
			FindByJavaName (peers, "my/app/GenericCallbackImpl").ImplementedInterfaceJavaNames);
	}

	[Fact]
	public void Scan_ComponentOnlyBase_BothBaseAndDerivedDiscovered ()
	{
		var peers = ScanFixtures ();

		var baseType = FindByJavaName (peers, "my/app/BaseActivityNoRegister");
		Assert.True (baseType.IsUnconditional);
		Assert.Equal ("android/app/Activity", baseType.BaseJavaName);

		var derived = FindByManagedName (peers, "MyApp.DerivedFromComponentBase");
		Assert.StartsWith ("crc64", derived.JavaName);
	}

	[Theory]
	[InlineData ("MyApp.RegisteredParent+UnregisteredChild", "my/app/RegisteredParent_UnregisteredChild")]
	[InlineData ("MyApp.DeepOuter+Middle+DeepInner", "my/app/DeepOuter_Middle_DeepInner")]
	public void Scan_UnregisteredNestedType_UsesParentJniPrefix (string managedName, string expectedJavaName)
	{
		var peers = ScanFixtures ();
		Assert.Equal (expectedJavaName, FindByManagedName (peers, managedName).JavaName);
	}

	[Fact]
	public void Scan_EmptyNamespace_Handled ()
	{
		var peers = ScanFixtures ();
		Assert.Equal ("GlobalType", FindByJavaName (peers, "my/app/GlobalType").ManagedTypeName);
		Assert.Equal ("GlobalUnregisteredType",
			FindByManagedName (peers, "GlobalUnregisteredType").CompatJniName);
	}

	[Theory]
	[InlineData ("MyApp.PlainActivitySubclass")]
	[InlineData ("MyApp.UnnamedActivity")]
	[InlineData ("MyApp.UnregisteredClickListener")]
	[InlineData ("MyApp.UnregisteredExporter")]
	public void Scan_UnregisteredType_DiscoveredWithCrc64Name (string managedName)
	{
		var peers = ScanFixtures ();
		Assert.StartsWith ("crc64", FindByManagedName (peers, managedName).JavaName);
	}

	[Fact]
	public void Scan_ExportOnUnregisteredType_MethodDiscovered ()
	{
		var peers = ScanFixtures ();
		var exportMethod = FindByManagedName (peers, "MyApp.UnregisteredExporter")
			.MarshalMethods.FirstOrDefault (m => m.JniName == "doExportedWork");
		Assert.NotNull (exportMethod);
		Assert.Null (exportMethod.Connector);
	}
}
