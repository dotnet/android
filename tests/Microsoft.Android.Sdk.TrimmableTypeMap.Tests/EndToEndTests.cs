using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// End-to-end tests that run the full pipeline:
/// Scanner → ComponentExtractor → AcwMapWriter → ManifestTypeInfo
/// Verifies that all pieces work together correctly on the TestFixtures assembly.
/// </summary>
public class EndToEndTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (EndToEndTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	[Fact]
	public void AcwMapContainsAllNonMcwTypes ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });
		var entries = AcwMapWriter.CreateEntries (peers, "TestFixtures");

		// User types should be present
		var managedKeys = entries.Select (e => e.ManagedKey).ToHashSet ();
		Assert.Contains ("MyApp.MainActivity", managedKeys);
		Assert.Contains ("MyApp.MyHelper", managedKeys);
		Assert.Contains ("MyApp.MyService", managedKeys);
		Assert.Contains ("MyApp.MyReceiver", managedKeys);
		Assert.Contains ("MyApp.MyProvider", managedKeys);
		Assert.Contains ("MyApp.DeepLinkActivity", managedKeys);
		Assert.Contains ("MyApp.MyApplication", managedKeys);
		Assert.Contains ("MyApp.MyInstrumentation", managedKeys);

		// MCW types should NOT be present
		Assert.DoesNotContain ("Java.Lang.Object", managedKeys);
		Assert.DoesNotContain ("Android.App.Activity", managedKeys);
		Assert.DoesNotContain ("Android.App.Service", managedKeys);
		Assert.DoesNotContain ("Android.Views.View", managedKeys);
	}

	[Fact]
	public void AcwMapWritesToFile ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });
		var entries = AcwMapWriter.CreateEntries (peers, "TestFixtures");

		var tempFile = Path.GetTempFileName ();
		try {
			var result = AcwMapWriter.WriteMapToFile (entries, tempFile);
			Assert.False (result.HasErrors);

			var content = File.ReadAllText (tempFile);
			Assert.Contains ("MyApp.MainActivity", content);
			Assert.Contains ("my.app.MainActivity", content);

			// Verify semicolon-separated format
			var lines = content.Split ('\n', System.StringSplitOptions.RemoveEmptyEntries);
			foreach (var line in lines) {
				Assert.Contains (';', line);
			}
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void AllComponentTypesDetected ()
	{
		var infos = ScannerManifestTypeInfoAdapter.ScanAndConvert (new [] { TestFixtureAssemblyPath });

		var componentTypes = infos.Where (i => i.ComponentKind != ManifestComponentKind.None).ToList ();

		// Verify we find all expected component kinds
		Assert.Contains (componentTypes, i => i.ComponentKind == ManifestComponentKind.Activity);
		Assert.Contains (componentTypes, i => i.ComponentKind == ManifestComponentKind.Service);
		Assert.Contains (componentTypes, i => i.ComponentKind == ManifestComponentKind.BroadcastReceiver);
		Assert.Contains (componentTypes, i => i.ComponentKind == ManifestComponentKind.ContentProvider);
		Assert.Contains (componentTypes, i => i.ComponentKind == ManifestComponentKind.Application);
		Assert.Contains (componentTypes, i => i.ComponentKind == ManifestComponentKind.Instrumentation);
	}

	[Fact]
	public void NonComponentTypes_HaveKindNone ()
	{
		var infos = ScannerManifestTypeInfoAdapter.ScanAndConvert (new [] { TestFixtureAssemblyPath });

		// Types without component attributes should have ComponentKind.None
		var helper = infos.First (i => i.FullName == "MyApp.MyHelper");
		Assert.Equal (ManifestComponentKind.None, helper.ComponentKind);

		var touchHandler = infos.First (i => i.FullName == "MyApp.TouchHandler");
		Assert.Equal (ManifestComponentKind.None, touchHandler.ComponentKind);
	}

	[Fact]
	public void ComponentInfoRoundTrip ()
	{
		var infos = ScannerManifestTypeInfoAdapter.ScanAndConvert (new [] { TestFixtureAssemblyPath });

		// Verify the deep link activity has all its sub-attributes intact
		var deepLink = infos.First (i => i.FullName == "MyApp.DeepLinkActivity");

		// Component attribute
		Assert.Equal (ManifestComponentKind.Activity, deepLink.ComponentKind);
		Assert.NotNull (deepLink.ComponentAttribute);
		Assert.Equal ("my.app.DeepLinkActivity", deepLink.ComponentAttribute!.Properties ["Name"]);

		// Intent filters
		Assert.Equal (2, deepLink.IntentFilters.Count);
		var viewFilter = deepLink.IntentFilters [0];
		var actions = (string [])viewFilter.ConstructorArguments [0];
		Assert.Contains ("android.intent.action.VIEW", actions);

		// MetaData
		Assert.Equal (2, deepLink.MetaDataEntries.Count);

		// Layout
		Assert.NotNull (deepLink.LayoutAttribute);

		// Property
		Assert.Single (deepLink.PropertyAttributes);
	}

	[Fact]
	public void ApplicationAttribute_PreservesTypeReferences ()
	{
		var infos = ScannerManifestTypeInfoAdapter.ScanAndConvert (new [] { TestFixtureAssemblyPath });
		var app = infos.First (i => i.FullName == "MyApp.MyApplication");

		Assert.NotNull (app.ComponentAttribute);
		// BackupAgent and ManageSpaceActivity are Type-valued props stored as type name strings
		Assert.True (app.ComponentAttribute!.Properties.ContainsKey ("BackupAgent"));
		Assert.True (app.ComponentAttribute.Properties.ContainsKey ("ManageSpaceActivity"));
	}

	[Fact]
	public void UnconditionalMarking_ComponentTypes ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		// Component types should be unconditional
		var mainActivity = peers.First (p => p.ManagedTypeName == "MyApp.MainActivity");
		Assert.True (mainActivity.IsUnconditional, "Activity type should be unconditional");

		var service = peers.First (p => p.ManagedTypeName == "MyApp.MyService");
		Assert.True (service.IsUnconditional, "Service type should be unconditional");

		var receiver = peers.First (p => p.ManagedTypeName == "MyApp.MyReceiver");
		Assert.True (receiver.IsUnconditional, "BroadcastReceiver type should be unconditional");

		var provider = peers.First (p => p.ManagedTypeName == "MyApp.MyProvider");
		Assert.True (provider.IsUnconditional, "ContentProvider type should be unconditional");

		var app = peers.First (p => p.ManagedTypeName == "MyApp.MyApplication");
		Assert.True (app.IsUnconditional, "Application type should be unconditional");

		var inst = peers.First (p => p.ManagedTypeName == "MyApp.MyInstrumentation");
		Assert.True (inst.IsUnconditional, "Instrumentation type should be unconditional");
	}

	[Fact]
	public void BackupAgentCrossReference_IsUnconditional ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		// MyBackupAgent is referenced by [Application(BackupAgent=typeof(MyBackupAgent))]
		var backupAgent = peers.First (p => p.ManagedTypeName == "MyApp.MyBackupAgent");
		Assert.True (backupAgent.IsUnconditional,
			"BackupAgent type referenced by [Application] should be unconditional");
	}

	[Fact]
	public void ManageSpaceActivityCrossReference_IsUnconditional ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		// MyManageSpaceActivity is referenced by [Application(ManageSpaceActivity=typeof(MyManageSpaceActivity))]
		var manageSpace = peers.First (p => p.ManagedTypeName == "MyApp.MyManageSpaceActivity");
		Assert.True (manageSpace.IsUnconditional,
			"ManageSpaceActivity type referenced by [Application] should be unconditional");
	}

	[Fact]
	public void HelperType_NotUnconditional ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var helper = peers.First (p => p.ManagedTypeName == "MyApp.MyHelper");
		Assert.False (helper.IsUnconditional, "Helper type without component attr should be trimmable");
	}
}
