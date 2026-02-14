using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
	[Fact]
	public void Scan_Context_IsDiscovered ()
	{
		var peers = ScanFixtures ();
		var context = FindByJavaName (peers, "android/content/Context");
		Assert.True (context.DoNotGenerateAcw, "Context is MCW");
		Assert.Equal ("java/lang/Object", context.BaseJavaName);
	}

	// ================================================================
	// Edge case tests — discovered during side-by-side testing
	// ================================================================

	[Fact]
	public void Scan_GenericBaseType_ResolvesViaTypeSpecification ()
	{
		// ConcreteFromGeneric extends GenericBase<string>. The base type
		// is a TypeSpecification (generic instantiation). The scanner must
		// decode the blob to resolve the underlying TypeDef/TypeRef.
		var peers = ScanFixtures ();
		var concrete = FindByJavaName (peers, "my/app/ConcreteFromGeneric");
		Assert.Equal ("my/app/GenericBase", concrete.BaseJavaName);
	}

	[Fact]
	public void Scan_GenericInterface_ResolvesViaTypeSpecification ()
	{
		// GenericCallbackImpl implements IGenericCallback<string>. The interface
		// is a TypeSpecification. The scanner must decode the blob to resolve it.
		var peers = ScanFixtures ();
		var impl = FindByJavaName (peers, "my/app/GenericCallbackImpl");
		Assert.Contains ("my/app/IGenericCallback", impl.ImplementedInterfaceJavaNames);
	}

	[Fact]
	public void Scan_ComponentOnlyBase_DerivedTypeIsDiscovered ()
	{
		// BaseActivityNoRegister has [Activity] but no [Register].
		// DerivedFromComponentBase extends it. ExtendsJavaPeerCore must
		// detect the component attribute on the base to include the derived type.
		var peers = ScanFixtures ();
		var derived = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.DerivedFromComponentBase");
		Assert.NotNull (derived);
		// Should get a CRC64-computed JNI name
		Assert.StartsWith ("crc64", derived.JavaName);
	}

	[Fact]
	public void Scan_ComponentOnlyBase_BaseTypeIsDiscovered ()
	{
		// BaseActivityNoRegister has [Activity(Name = "...")] — should be discovered
		// even without [Register].
		var peers = ScanFixtures ();
		var baseType = FindByJavaName (peers, "my/app/BaseActivityNoRegister");
		Assert.True (baseType.IsUnconditional, "[Activity] makes it unconditional");
		Assert.Equal ("android/app/Activity", baseType.BaseJavaName);
	}

	[Fact]
	public void Scan_UnregisteredNestedType_UsesParentJniPrefix ()
	{
		// UnregisteredChild has no [Register] but its parent RegisteredParent does.
		// ComputeTypeNameParts should use parent's JNI name as prefix.
		var peers = ScanFixtures ();
		var child = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.RegisteredParent+UnregisteredChild");
		Assert.NotNull (child);
		Assert.Equal ("my/app/RegisteredParent_UnregisteredChild", child.JavaName);
	}

	[Fact]
	public void Scan_EmptyNamespace_RegisteredType_Discovered ()
	{
		// GlobalType has [Register] and no namespace — should work normally.
		var peers = ScanFixtures ();
		var global = FindByJavaName (peers, "my/app/GlobalType");
		Assert.Equal ("GlobalType", global.ManagedTypeName);
	}

	[Fact]
	public void Scan_EmptyNamespace_UnregisteredType_CompatJniHasNoSlash ()
	{
		// GlobalUnregisteredType has no namespace and no [Register].
		// CompatJniName should just be the type name (no leading slash).
		var peers = ScanFixtures ();
		var global = peers.FirstOrDefault (p => p.ManagedTypeName == "GlobalUnregisteredType");
		Assert.NotNull (global);
		Assert.Equal ("GlobalUnregisteredType", global.CompatJniName);
		Assert.DoesNotContain ("/", global.CompatJniName);
	}

	[Fact]
	public void Scan_DeepNestedType_ThreeLevelNesting ()
	{
		// DeepOuter.Middle.DeepInner — 3-level nesting.
		// ComputeTypeNameParts walks multiple declaring-type levels.
		var peers = ScanFixtures ();
		var deep = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.DeepOuter+Middle+DeepInner");
		Assert.NotNull (deep);
		Assert.Equal ("my/app/DeepOuter_Middle_DeepInner", deep.JavaName);
	}

	[Fact]
	public void Scan_PlainActivitySubclass_DiscoveredWithCrc64Name ()
	{
		// PlainActivitySubclass extends Activity with no [Register], no [Activity].
		// ExtendsJavaPeer detects it via the base type chain, gets CRC64 name.
		var peers = ScanFixtures ();
		var plain = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.PlainActivitySubclass");
		Assert.NotNull (plain);
		Assert.StartsWith ("crc64", plain.JavaName);
		Assert.Equal ("android/app/Activity", plain.BaseJavaName);
	}

	[Fact]
	public void Scan_ComponentAttributeWithoutName_DiscoveredWithCrc64Name ()
	{
		// UnnamedActivity has [Activity(Label="Unnamed")] but no Name property.
		// HasComponentAttribute = true, ComponentAttributeJniName should be null,
		// and the type should still get a CRC64-based JNI name.
		var peers = ScanFixtures ();
		var unnamed = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.UnnamedActivity");
		Assert.NotNull (unnamed);
		Assert.StartsWith ("crc64", unnamed.JavaName);
		Assert.True (unnamed.IsUnconditional, "[Activity] makes it unconditional");
	}

	[Fact]
	public void Scan_InterfaceOnUnregisteredType_InterfacesResolved ()
	{
		// UnregisteredClickListener has no [Register] but implements IOnClickListener.
		// Type gets CRC64 name, interfaces still resolved.
		var peers = ScanFixtures ();
		var listener = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.UnregisteredClickListener");
		Assert.NotNull (listener);
		Assert.StartsWith ("crc64", listener.JavaName);
		Assert.Contains ("android/view/View$OnClickListener", listener.ImplementedInterfaceJavaNames);
	}

	[Fact]
	public void Scan_ExportOnUnregisteredType_MethodDiscovered ()
	{
		// UnregisteredExporter has [Export("doExportedWork")] on a type without [Register].
		// Type gets CRC64 name, [Export] method is in MarshalMethods.
		var peers = ScanFixtures ();
		var exporter = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.UnregisteredExporter");
		Assert.NotNull (exporter);
		Assert.StartsWith ("crc64", exporter.JavaName);
		var exportMethod = exporter.MarshalMethods.FirstOrDefault (m => m.JniName == "doExportedWork");
		Assert.NotNull (exportMethod);
		Assert.Null (exportMethod.Connector);
	}
}
