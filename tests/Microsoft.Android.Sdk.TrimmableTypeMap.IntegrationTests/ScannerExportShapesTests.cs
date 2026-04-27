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
}
