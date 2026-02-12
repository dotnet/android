using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ComponentAttributeExtractorTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (ComponentAttributeExtractorTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	(MetadataReader reader, PEReader peReader) OpenFixtureAssembly ()
	{
		var peReader = new PEReader (File.OpenRead (TestFixtureAssemblyPath));
		var reader = peReader.GetMetadataReader ();
		return (reader, peReader);
	}

	TypeDefinitionHandle FindType (MetadataReader reader, string fullName)
	{
		foreach (var typeHandle in reader.TypeDefinitions) {
			var typeDef = reader.GetTypeDefinition (typeHandle);
			var ns = reader.GetString (typeDef.Namespace);
			var name = reader.GetString (typeDef.Name);
			var fn = ns.Length > 0 ? ns + "." + name : name;
			if (fn == fullName)
				return typeHandle;
		}
		Assert.Fail ($"Type '{fullName}' not found in TestFixtures.dll");
		return default;
	}

	[Fact]
	public void ExtractsActivityAttribute ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MainActivity");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (ManifestComponentKind.Activity, data.ComponentKind);
			Assert.NotNull (data.ComponentAttribute);
			Assert.Equal ("Android.App.ActivityAttribute", data.ComponentAttribute!.AttributeType);

			var props = data.ComponentAttribute.Properties;
			Assert.True ((bool)props ["MainLauncher"]);
			Assert.Equal ("My App", props ["Label"]);
			Assert.Equal ("my.app.MainActivity", props ["Name"]);
		}
	}

	[Fact]
	public void ExtractsActivityWithIntentFilters ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.DeepLinkActivity");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (ManifestComponentKind.Activity, data.ComponentKind);
			Assert.Equal (2, data.IntentFilters.Count);

			// First intent filter: VIEW action with deep link data
			var viewFilter = data.IntentFilters [0];
			Assert.Equal ("Android.App.IntentFilterAttribute", viewFilter.AttributeType);
			Assert.Single (viewFilter.ConstructorArguments);
			var actions = (string [])viewFilter.ConstructorArguments [0];
			Assert.Contains ("android.intent.action.VIEW", actions);
			var categories = (string [])viewFilter.Properties ["Categories"];
			Assert.Contains ("android.intent.category.BROWSABLE", categories);
			Assert.Equal ("https", viewFilter.Properties ["DataScheme"]);
			Assert.Equal ("example.com", viewFilter.Properties ["DataHost"]);
			Assert.True ((bool)viewFilter.Properties ["AutoVerify"]);

			// Second intent filter: custom action
			var customFilter = data.IntentFilters [1];
			var customActions = (string [])customFilter.ConstructorArguments [0];
			Assert.Contains ("my.app.CUSTOM_ACTION", customActions);
		}
	}

	[Fact]
	public void ExtractsActivityWithMetaData ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.DeepLinkActivity");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (2, data.MetaDataEntries.Count);

			var apiKey = data.MetaDataEntries.First (m =>
				m.ConstructorArguments.Count > 0 &&
				m.ConstructorArguments [0].ToString () == "com.google.android.geo.API_KEY");
			Assert.Equal ("test-api-key", apiKey.Properties ["Value"]);

			var gmsVersion = data.MetaDataEntries.First (m =>
				m.ConstructorArguments.Count > 0 &&
				m.ConstructorArguments [0].ToString () == "com.google.android.gms.version");
			Assert.Equal ("@integer/google_play_services_version", gmsVersion.Properties ["Resource"]);
		}
	}

	[Fact]
	public void ExtractsActivityWithLayout ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.DeepLinkActivity");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.NotNull (data.LayoutAttribute);
			Assert.Equal ("500dp", data.LayoutAttribute!.Properties ["DefaultWidth"]);
			Assert.Equal ("600dp", data.LayoutAttribute.Properties ["DefaultHeight"]);
			Assert.Equal ("center", data.LayoutAttribute.Properties ["Gravity"]);
			Assert.Equal ("300dp", data.LayoutAttribute.Properties ["MinWidth"]);
			Assert.Equal ("400dp", data.LayoutAttribute.Properties ["MinHeight"]);
		}
	}

	[Fact]
	public void ExtractsActivityWithProperty ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.DeepLinkActivity");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Single (data.PropertyAttributes);
			var prop = data.PropertyAttributes [0];
			Assert.Equal ("custom.prop", prop.ConstructorArguments [0]);
			Assert.Equal ("custom-value", prop.Properties ["Value"]);
		}
	}

	[Fact]
	public void ExtractsServiceAttribute ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyService");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (ManifestComponentKind.Service, data.ComponentKind);
			Assert.NotNull (data.ComponentAttribute);
			Assert.Equal ("my.app.MyService", data.ComponentAttribute!.Properties ["Name"]);
			Assert.True ((bool)data.ComponentAttribute.Properties ["Exported"]);
			Assert.Equal ("my.app.BIND_SERVICE", data.ComponentAttribute.Properties ["Permission"]);
			Assert.True ((bool)data.ComponentAttribute.Properties ["IsolatedProcess"]);
		}
	}

	[Fact]
	public void ExtractsServiceWithIntentFilter ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyService");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Single (data.IntentFilters);
			var actions = (string [])data.IntentFilters [0].ConstructorArguments [0];
			Assert.Contains ("my.app.START_SERVICE", actions);
		}
	}

	[Fact]
	public void ExtractsBroadcastReceiverAttribute ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyReceiver");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (ManifestComponentKind.BroadcastReceiver, data.ComponentKind);
			Assert.NotNull (data.ComponentAttribute);
			Assert.Equal ("my.app.MyReceiver", data.ComponentAttribute!.Properties ["Name"]);
			Assert.True ((bool)data.ComponentAttribute.Properties ["Exported"]);
			Assert.Equal ("my.app.RECEIVE_BROADCAST", data.ComponentAttribute.Properties ["Permission"]);
		}
	}

	[Fact]
	public void ExtractsContentProviderAttribute ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyProvider");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (ManifestComponentKind.ContentProvider, data.ComponentKind);
			Assert.NotNull (data.ComponentAttribute);
			Assert.Equal ("my.app.MyProvider", data.ComponentAttribute!.Properties ["Name"]);

			// Constructor argument: authorities string[]
			Assert.Single (data.ComponentAttribute.ConstructorArguments);
			var authorities = (string [])data.ComponentAttribute.ConstructorArguments [0];
			Assert.Contains ("my.app.provider", authorities);

			Assert.True ((bool)data.ComponentAttribute.Properties ["Exported"]);
			Assert.True ((bool)data.ComponentAttribute.Properties ["GrantUriPermissions"]);
		}
	}

	[Fact]
	public void ExtractsContentProviderWithGrantUri ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyProvider");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (2, data.GrantUriPermissions.Count);
			Assert.Contains (data.GrantUriPermissions, g => g.Properties.ContainsKey ("Path") && (string)g.Properties ["Path"] == "/data");
			Assert.Contains (data.GrantUriPermissions, g => g.Properties.ContainsKey ("PathPrefix") && (string)g.Properties ["PathPrefix"] == "/files");
		}
	}

	[Fact]
	public void ExtractsApplicationAttribute ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyApplication");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (ManifestComponentKind.Application, data.ComponentKind);
			Assert.NotNull (data.ComponentAttribute);
			Assert.Equal ("my.app.MyApplication", data.ComponentAttribute!.Properties ["Name"]);
			Assert.True ((bool)data.ComponentAttribute.Properties ["Debuggable"]);
			Assert.True ((bool)data.ComponentAttribute.Properties ["AllowBackup"]);
			Assert.True ((bool)data.ComponentAttribute.Properties ["SupportsRtl"]);
			Assert.Equal ("@style/AppTheme", data.ComponentAttribute.Properties ["Theme"]);
			Assert.Equal ("My Application", data.ComponentAttribute.Properties ["Label"]);
			Assert.Equal ("@mipmap/ic_launcher", data.ComponentAttribute.Properties ["Icon"]);
		}
	}

	[Fact]
	public void ExtractsApplicationAttribute_TypeProperties ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyApplication");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			// Type-valued properties are decoded as type name strings by CustomAttributeTypeProvider
			Assert.True (data.ComponentAttribute!.Properties.ContainsKey ("BackupAgent"));
			Assert.True (data.ComponentAttribute.Properties.ContainsKey ("ManageSpaceActivity"));
		}
	}

	[Fact]
	public void ExtractsApplicationWithMetaData ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyApplication");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Single (data.MetaDataEntries);
			Assert.Equal ("app.version", data.MetaDataEntries [0].ConstructorArguments [0]);
			Assert.Equal ("2.0", data.MetaDataEntries [0].Properties ["Value"]);
		}
	}

	[Fact]
	public void ExtractsInstrumentationAttribute ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyInstrumentation");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (ManifestComponentKind.Instrumentation, data.ComponentKind);
			Assert.NotNull (data.ComponentAttribute);
			Assert.Equal ("my.app.MyInstrumentation", data.ComponentAttribute!.Properties ["Name"]);
			Assert.Equal ("my.app", data.ComponentAttribute.Properties ["TargetPackage"]);
			Assert.True ((bool)data.ComponentAttribute.Properties ["FunctionalTest"]);
			Assert.True ((bool)data.ComponentAttribute.Properties ["HandleProfiling"]);
			Assert.Equal ("Test Runner", data.ComponentAttribute.Properties ["Label"]);
		}
	}

	[Fact]
	public void NoComponentAttribute_ReturnsNone ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyHelper");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (ManifestComponentKind.None, data.ComponentKind);
			Assert.Null (data.ComponentAttribute);
			Assert.Empty (data.IntentFilters);
			Assert.Empty (data.MetaDataEntries);
		}
	}

	[Fact]
	public void McwBindingType_NoComponentAttribute ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "Android.App.Activity");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			Assert.Equal (ManifestComponentKind.None, data.ComponentKind);
			Assert.Null (data.ComponentAttribute);
		}
	}

	[Fact]
	public void BooleanProperties_DecodedCorrectly ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.DeepLinkActivity");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			// Exported is a boolean property
			Assert.True ((bool)data.ComponentAttribute!.Properties ["Exported"]);
		}
	}

	[Fact]
	public void StringArrayProperties_DecodedCorrectly ()
	{
		var (reader, pe) = OpenFixtureAssembly ();
		using (pe) {
			var typeHandle = FindType (reader, "MyApp.MyProvider");
			var data = ComponentAttributeExtractor.Extract (reader, typeHandle);

			// ContentProvider constructor takes string[] authorities
			var authorities = (string [])data.ComponentAttribute!.ConstructorArguments [0];
			Assert.Single (authorities);
			Assert.Equal ("my.app.provider", authorities [0]);
		}
	}
}
