using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
[Fact]
public void Scan_GenericBaseType_ResolvesViaTypeSpecification ()
{
var peers = ScanFixtures ();
Assert.Equal ("my/app/GenericBase",
FindByJavaName (peers, "my/app/ConcreteFromGeneric").BaseJavaName);
}

[Fact]
public void Scan_GenericInterface_ResolvesViaTypeSpecification ()
{
var peers = ScanFixtures ();
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

[Fact]
public void Scan_UnregisteredNestedType_UsesParentJniPrefix ()
{
var peers = ScanFixtures ();
var child = FindByManagedName (peers, "MyApp.RegisteredParent+UnregisteredChild");
Assert.Equal ("my/app/RegisteredParent_UnregisteredChild", child.JavaName);
}

[Fact]
public void Scan_DeepNestedType_ThreeLevelNesting ()
{
var peers = ScanFixtures ();
var deep = FindByManagedName (peers, "MyApp.DeepOuter+Middle+DeepInner");
Assert.Equal ("my/app/DeepOuter_Middle_DeepInner", deep.JavaName);
}

[Fact]
public void Scan_EmptyNamespace_RegisteredType_Discovered ()
{
var peers = ScanFixtures ();
Assert.Equal ("GlobalType", FindByJavaName (peers, "my/app/GlobalType").ManagedTypeName);
}

[Fact]
public void Scan_EmptyNamespace_UnregisteredType_CompatJniHasNoSlash ()
{
var peers = ScanFixtures ();
var global = FindByManagedName (peers, "GlobalUnregisteredType");
Assert.Equal ("GlobalUnregisteredType", global.CompatJniName);
}

[Fact]
public void Scan_PlainActivitySubclass_DiscoveredWithCrc64Name ()
{
var peers = ScanFixtures ();
var plain = FindByManagedName (peers, "MyApp.PlainActivitySubclass");
Assert.StartsWith ("crc64", plain.JavaName);
Assert.Equal ("android/app/Activity", plain.BaseJavaName);
}

[Fact]
public void Scan_ComponentAttributeWithoutName_DiscoveredWithCrc64Name ()
{
var peers = ScanFixtures ();
var unnamed = FindByManagedName (peers, "MyApp.UnnamedActivity");
Assert.StartsWith ("crc64", unnamed.JavaName);
Assert.True (unnamed.IsUnconditional);
}

[Fact]
public void Scan_InterfaceOnUnregisteredType_InterfacesResolved ()
{
var peers = ScanFixtures ();
var listener = FindByManagedName (peers, "MyApp.UnregisteredClickListener");
Assert.StartsWith ("crc64", listener.JavaName);
Assert.Contains ("android/view/View$OnClickListener", listener.ImplementedInterfaceJavaNames);
}

[Fact]
public void Scan_ExportOnUnregisteredType_MethodDiscovered ()
{
var peers = ScanFixtures ();
var exporter = FindByManagedName (peers, "MyApp.UnregisteredExporter");
Assert.StartsWith ("crc64", exporter.JavaName);
var exportMethod = exporter.MarshalMethods.FirstOrDefault (m => m.JniName == "doExportedWork");
Assert.NotNull (exportMethod);
Assert.Null (exportMethod.Connector);
}
}
