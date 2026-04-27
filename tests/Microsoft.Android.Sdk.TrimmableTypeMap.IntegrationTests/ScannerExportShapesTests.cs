using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.IntegrationTests;

/// <summary>
/// Integration coverage for the trimmable scanner's [Export] handling on
/// shapes that the legacy JCW emitter (CecilImporter.GetJniSignature) cannot
/// encode: enum-typed parameters / returns, ICharSequence, and non-generic
/// IList / IDictionary / ICollection. ScannerComparisonTests.RunLegacy falls
/// back to direct [Register] extraction for these types (yields no entries),
/// so legacy↔new comparison is intentionally skipped — these tests assert
/// the new scanner produces the right JNI signatures end-to-end.
/// </summary>
public class ScannerExportShapesTests
{
	static string UserTypesFixturePath {
		get {
			var testDir = Path.GetDirectoryName (typeof (ScannerExportShapesTests).Assembly.Location)
				?? throw new System.InvalidOperationException ("Could not determine test assembly directory.");
			var path = Path.Combine (testDir, "UserTypesFixture.dll");
			Assert.True (File.Exists (path), $"UserTypesFixture.dll not found at '{path}'.");
			return path;
		}
	}

	static MarshalMethodInfo[] GetMarshalMethods (string javaName)
	{
		var fixturePath = UserTypesFixturePath;
		var dir = Path.GetDirectoryName (fixturePath)!;

		var paths = new System.Collections.Generic.List<string> { fixturePath };
		var monoAndroid = Path.Combine (dir, "Mono.Android.dll");
		var javaInterop = Path.Combine (dir, "Java.Interop.dll");
		if (File.Exists (monoAndroid))
			paths.Add (monoAndroid);
		if (File.Exists (javaInterop))
			paths.Add (javaInterop);

		using var scanner = new JavaPeerScanner ();
		var peReaders = new System.Collections.Generic.List<PEReader> ();
		try {
			var assemblies = new System.Collections.Generic.List<(string Name, PEReader Reader)> ();
			foreach (var p in paths) {
				var pe = new PEReader (File.OpenRead (p));
				peReaders.Add (pe);
				var md = pe.GetMetadataReader ();
				assemblies.Add ((md.GetString (md.GetAssemblyDefinition ().Name), pe));
			}

			var peers = scanner.Scan (assemblies);
			var peer = peers.FirstOrDefault (p => p.ManagedTypeName.EndsWith (javaName));
			Assert.NotNull (peer);
			return peer!.MarshalMethods.ToArray ();
		} finally {
			foreach (var pe in peReaders)
				pe.Dispose ();
		}
	}

	static void AssertHasExport (MarshalMethodInfo[] methods, string jniName, string jniSignature)
	{
		var match = methods.FirstOrDefault (m => m.JniName == jniName && m.JniSignature == jniSignature);
		Assert.True (match != null,
			$"Expected [Export] marshal method '{jniName}{jniSignature}' not found. " +
			$"Discovered: {string.Join (", ", methods.Select (m => m.JniName + m.JniSignature))}");
		// [Export] methods carry no Connector — legacy uses __export__ at runtime,
		// trimmable wires registration via UCO fnptr. [ExportField] methods do
		// surface the "__export__" connector by design (matches legacy
		// CecilImporter behaviour), so accept that case too.
		Assert.True (match!.Connector is null || match.Connector == "__export__",
			$"Unexpected connector '{match.Connector}' on {jniName}{jniSignature}.");
	}

	[Fact]
	public void EnumParam_AndReturn_MarshalAsUnderlyingPrimitive ()
	{
		var methods = GetMarshalMethods ("ExportEnumShapes");

		// SampleEnum (Int32) → I
		AssertHasExport (methods, "echoEnum", "(I)I");
		// SampleByteEnum → B
		AssertHasExport (methods, "echoByteEnum", "(B)B");
		// SampleLongEnum → J
		AssertHasExport (methods, "echoLongEnum", "(J)J");
	}

	[Fact]
	public void ICharSequenceParam_AndReturn_MarshalsAsCharSequence ()
	{
		var methods = GetMarshalMethods ("ExportCharSequenceShapes");
		AssertHasExport (methods, "echoCharSequence", "(Ljava/lang/CharSequence;)Ljava/lang/CharSequence;");
	}

	[Fact]
	public void NonGenericCollections_MarshalAsExpectedJavaTypes ()
	{
		var methods = GetMarshalMethods ("ExportCollectionShapes");

		AssertHasExport (methods, "echoList", "(Ljava/util/List;)Ljava/util/List;");
		AssertHasExport (methods, "echoMap", "(Ljava/util/Map;)Ljava/util/Map;");
		AssertHasExport (methods, "echoCollection", "(Ljava/util/Collection;)Ljava/util/Collection;");
	}

	[Fact]
	public void ExportField_RegistersGetterAsMarshalMethod ()
	{
		var methods = GetMarshalMethods ("ExportFieldShapes");

		// [ExportField] uses the managed method name as the JNI method name
		// (legacy Mono.Android.Export does the same thing). The signatures
		// below match the underlying CLR method shape.
		// User-peer return type uses a CRC64-based package name; assert by prefix
		// so the test isn't tied to the exact CRC64 hash of the assembly.
		var getInstance = System.Array.Find (methods, m => m.JniName == "GetInstance");
		Assert.NotNull (getInstance);
		Assert.EndsWith ("/ExportFieldShapes;", getInstance!.JniSignature);
		Assert.StartsWith ("()L", getInstance.JniSignature);
		Assert.DoesNotContain ("Ljava/lang/Object;", getInstance.JniSignature);

		AssertHasExport (methods, "GetValue", "()Ljava/lang/String;");
		AssertHasExport (methods, "GetCount", "()I");
	}

	[Fact]
	public void ExportParameter_OverridesJavaTypeForStreamsAndXml ()
	{
		var methods = GetMarshalMethods ("ExportParameterShapes");

		// Stream → InputStream / OutputStream
		AssertHasExport (methods, "openStream", "(Ljava/io/InputStream;)I");
		AssertHasExport (methods, "wrapStream", "(Ljava/io/OutputStream;)Ljava/io/OutputStream;");
		// XmlReader → XmlPullParser / XmlResourceParser
		AssertHasExport (methods, "readXml", "(Lorg/xmlpull/v1/XmlPullParser;)Lorg/xmlpull/v1/XmlPullParser;");
		AssertHasExport (methods, "readResourceXml", "(Landroid/content/res/XmlResourceParser;)Landroid/content/res/XmlResourceParser;");
	}

	// === Phase A: dispatch & declaration shapes ===

	[Fact]
	public void StaticExport_RegistersStaticDispatch ()
	{
		var methods = GetMarshalMethods ("StaticExportShapes");
		AssertHasExport (methods, "compute", "(I)I");
		AssertHasExport (methods, "hello", "()Ljava/lang/String;");
	}

	[Fact]
	public void Export_WithThrowsClause_SurfacesDeclaredExceptions ()
	{
		var methods = GetMarshalMethods ("ExportThrowsShapes");

		var ioCall = System.Array.Find (methods, m => m.JniName == "ioCall");
		Assert.NotNull (ioCall);
		Assert.NotNull (ioCall!.ThrownNames);
		Assert.Contains ("java/io/IOException", ioCall.ThrownNames!);

		var multiThrow = System.Array.Find (methods, m => m.JniName == "multiThrow");
		Assert.NotNull (multiThrow);
		Assert.NotNull (multiThrow!.ThrownNames);
		Assert.Contains ("java/io/IOException", multiThrow.ThrownNames!);
		Assert.Contains ("java/lang/IllegalStateException", multiThrow.ThrownNames!);
	}

	[Fact]
	public void MixedRegisterAndExport_BothPathsSurface ()
	{
		var methods = GetMarshalMethods ("MixedRegisterAndExport");

		// [Register]-driven Activity override carries a connector
		var onCreate = System.Array.Find (methods, m => m.JniName == "onCreate");
		Assert.NotNull (onCreate);
		Assert.False (onCreate!.Connector is null or "__export__",
			$"OnCreate override should have a real Get*Handler connector, got '{onCreate.Connector}'.");

		// [Export]-driven new methods carry no connector (or "__export__")
		AssertHasExport (methods, "doWork", "()V");
		AssertHasExport (methods, "compute", "(I)I");
	}

	[Fact]
	public void VirtualExport_TopMostDeclarationRegisters ()
	{
		var baseMethods = GetMarshalMethods ("VirtualExportBase");
		AssertHasExport (baseMethods, "ping", "()I");

		var derivedMethods = GetMarshalMethods ("VirtualExportDerived");
		// Derived class doesn't re-declare [Export]; only the base [Export] applies,
		// so the derived peer should NOT add a duplicate marshal-method entry of its
		// own. (Legacy CecilImporter walks up the inheritance chain and registers
		// the [Export] on the topmost declaring type.)
		var derivedPing = System.Array.FindAll (derivedMethods, m => m.JniName == "ping");
		Assert.True (derivedPing.Length <= 1,
			$"Derived peer should not duplicate base's [Export] entry, found {derivedPing.Length}.");
	}

	[Fact]
	public void Export_CustomJniName_NotIdentityMappedFromMethodName ()
	{
		var methods = GetMarshalMethods ("ExportRenameShapes");

		// JNI name comes from [Export("javaSideName")], not from "CSharpSideName".
		Assert.Contains (methods, m => m.JniName == "javaSideName" && m.JniSignature == "()V");
		Assert.DoesNotContain (methods, m => m.JniName == "CSharpSideName");
	}

	// === Phase B: edge marshalling ===

	[Fact]
	public void Export_JavaLangObjectExplicitly_KeepsObjectDescriptor ()
	{
		var methods = GetMarshalMethods ("ExportObjectShapes");
		AssertHasExport (methods, "any", "(Ljava/lang/Object;)Ljava/lang/Object;");
	}

	[Fact]
	public void Export_ArrayOfUserPeerType_RecursesUserPeerResolver ()
	{
		var methods = GetMarshalMethods ("ExportUserPeerArrayShapes");
		var echoArr = System.Array.Find (methods, m => m.JniName == "echoArr");
		Assert.NotNull (echoArr);
		// Both parameter and return are arrays of the user-peer UserPeerForArray.
		// CRC64 hash is environment-dependent; assert by suffix.
		Assert.Matches (@"^\(\[Lcrc64[0-9a-f]{16}/UserPeerForArray;\)\[Lcrc64[0-9a-f]{16}/UserPeerForArray;$", echoArr!.JniSignature);
	}

	[Fact]
	public void Export_ProtectedAndPrivateVisibility_BothSurface ()
	{
		var methods = GetMarshalMethods ("ExportVisibilityShapes");
		AssertHasExport (methods, "doProtected", "()V");
		AssertHasExport (methods, "doPrivate", "()V");
	}

	[Fact]
	public void ExportField_ReturningPrimitive ()
	{
		var methods = GetMarshalMethods ("ExportFieldPrimitiveShapes");
		// [ExportField] uses the managed method name as the JNI name (not the field name).
		var getMaxValue = System.Array.Find (methods, m => m.JniName == "GetMaxValue");
		Assert.NotNull (getMaxValue);
		Assert.Equal ("()I", getMaxValue!.JniSignature);
		Assert.Equal ("__export__", getMaxValue.Connector);
	}

	[Fact]
	public void Export_OverloadsWithSameJavaName_RegisterDistinctly ()
	{
		var methods = GetMarshalMethods ("ExportOverloadShapes");
		var calls = System.Array.FindAll (methods, m => m.JniName == "call");
		Assert.Equal (2, calls.Length);
		Assert.Contains (calls, m => m.JniSignature == "(I)V");
		Assert.Contains (calls, m => m.JniSignature == "(Ljava/lang/String;)V");
	}

	// === Phase C: robustness ===

	[Fact]
	public void Export_GenericMethod_ScannerDoesNotCrash ()
	{
		// Generic methods aren't legal Java targets for [Export], but the
		// scanner must not crash. Either the method is skipped or it surfaces
		// with some defined fallback — assert only that we get a non-null
		// peer back without throwing.
		var methods = GetMarshalMethods ("ExportGenericShapes");
		Assert.NotNull (methods);
	}

	[Fact]
	public void Export_OnRegisterOverride_RegisterPathWins ()
	{
		var methods = GetMarshalMethods ("ExportOverridingRegisterShape");

		// The Activity.OnCreate override carries [Register]-driven dispatch
		// (real Get*Handler connector). Putting [Export] on top of an override
		// of a [Register]'d base means BOTH entries are registered: the
		// [Register]-driven override (so Activity.onCreate dispatch still works)
		// AND the [Export]-driven new method (so Java callers can call the
		// renamed method). Matches legacy CecilImporter behaviour.
		var onCreate = System.Array.Find (methods, m => m.JniName == "onCreate");
		Assert.NotNull (onCreate);
		Assert.False (onCreate!.Connector is null or "__export__",
			$"OnCreate override should keep its [Register]-driven Get*Handler connector, got '{onCreate.Connector}'.");

		var onCreateExport = System.Array.Find (methods, m => m.JniName == "onCreateExport");
		Assert.NotNull (onCreateExport);
		Assert.True (onCreateExport!.Connector is null or "__export__",
			$"[Export]-driven entry should have no real connector, got '{onCreateExport.Connector}'.");
	}
}
