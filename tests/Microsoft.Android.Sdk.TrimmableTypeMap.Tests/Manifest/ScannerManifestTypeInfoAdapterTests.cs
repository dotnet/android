using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ScannerManifestTypeInfoAdapterTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (ScannerManifestTypeInfoAdapterTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	List<IManifestTypeInfo> ScanAndConvert ()
	{
		return ScannerManifestTypeInfoAdapter.ScanAndConvert (new [] { TestFixtureAssemblyPath });
	}

	IManifestTypeInfo FindByFullName (List<IManifestTypeInfo> infos, string fullName)
	{
		var info = infos.FirstOrDefault (i => i.FullName == fullName);
		Assert.NotNull (info);
		return info;
	}

	[Fact]
	public void ConvertsActivityType ()
	{
		var infos = ScanAndConvert ();
		var activity = FindByFullName (infos, "MyApp.MainActivity");

		Assert.Equal (ManifestComponentKind.Activity, activity.ComponentKind);
		Assert.Equal ("my.app.MainActivity", activity.JavaName);
		Assert.False (activity.IsAbstract);
		Assert.NotNull (activity.ComponentAttribute);
		Assert.True ((bool)activity.ComponentAttribute!.Properties ["MainLauncher"]);
	}

	[Fact]
	public void ConvertsServiceType ()
	{
		var infos = ScanAndConvert ();
		var service = FindByFullName (infos, "MyApp.MyService");

		Assert.Equal (ManifestComponentKind.Service, service.ComponentKind);
		Assert.NotNull (service.ComponentAttribute);
		Assert.Equal ("my.app.MyService", service.ComponentAttribute!.Properties ["Name"]);
	}

	[Fact]
	public void ConvertsBroadcastReceiverType ()
	{
		var infos = ScanAndConvert ();
		var receiver = FindByFullName (infos, "MyApp.MyReceiver");

		Assert.Equal (ManifestComponentKind.BroadcastReceiver, receiver.ComponentKind);
	}

	[Fact]
	public void ConvertsContentProviderType ()
	{
		var infos = ScanAndConvert ();
		var provider = FindByFullName (infos, "MyApp.MyProvider");

		Assert.Equal (ManifestComponentKind.ContentProvider, provider.ComponentKind);
		Assert.NotEmpty (provider.GrantUriPermissions);
	}

	[Fact]
	public void ConvertsApplicationType ()
	{
		var infos = ScanAndConvert ();
		var app = FindByFullName (infos, "MyApp.MyApplication");

		Assert.Equal (ManifestComponentKind.Application, app.ComponentKind);
	}

	[Fact]
	public void ConvertsInstrumentationType ()
	{
		var infos = ScanAndConvert ();
		var inst = FindByFullName (infos, "MyApp.MyInstrumentation");

		Assert.Equal (ManifestComponentKind.Instrumentation, inst.ComponentKind);
	}

	[Fact]
	public void NonComponentType_HasKindNone ()
	{
		var infos = ScanAndConvert ();
		var helper = FindByFullName (infos, "MyApp.MyHelper");

		Assert.Equal (ManifestComponentKind.None, helper.ComponentKind);
		Assert.Null (helper.ComponentAttribute);
	}

	[Fact]
	public void ExcludesDoNotGenerateAcwTypes ()
	{
		var infos = ScanAndConvert ();

		// MCW types with DoNotGenerateAcw should not be in the result
		Assert.DoesNotContain (infos, i => i.FullName == "Java.Lang.Object");
		Assert.DoesNotContain (infos, i => i.FullName == "Android.App.Activity");
	}

	[Fact]
	public void PreservesIntentFilters ()
	{
		var infos = ScanAndConvert ();
		var deepLink = FindByFullName (infos, "MyApp.DeepLinkActivity");

		Assert.Equal (2, deepLink.IntentFilters.Count);
	}

	[Fact]
	public void PreservesMetaData ()
	{
		var infos = ScanAndConvert ();
		var deepLink = FindByFullName (infos, "MyApp.DeepLinkActivity");

		Assert.Equal (2, deepLink.MetaDataEntries.Count);
	}

	[Fact]
	public void PreservesLayoutAttribute ()
	{
		var infos = ScanAndConvert ();
		var deepLink = FindByFullName (infos, "MyApp.DeepLinkActivity");

		Assert.NotNull (deepLink.LayoutAttribute);
		Assert.Equal ("500dp", deepLink.LayoutAttribute!.Properties ["DefaultWidth"]);
	}

	[Fact]
	public void PreservesPropertyAttributes ()
	{
		var infos = ScanAndConvert ();
		var deepLink = FindByFullName (infos, "MyApp.DeepLinkActivity");

		Assert.Single (deepLink.PropertyAttributes);
	}

	[Fact]
	public void PreservesGrantUriPermissions ()
	{
		var infos = ScanAndConvert ();
		var provider = FindByFullName (infos, "MyApp.MyProvider");

		Assert.Equal (2, provider.GrantUriPermissions.Count);
	}

	[Fact]
	public void AbstractType_IsAbstractTrue ()
	{
		var infos = ScanAndConvert ();
		var baseActivity = FindByFullName (infos, "MyApp.BaseActivity");

		Assert.True (baseActivity.IsAbstract);
		Assert.Equal (ManifestComponentKind.Activity, baseActivity.ComponentKind);
	}

	[Fact]
	public void AllActivityProperties_Preserved ()
	{
		var infos = ScanAndConvert ();
		var deepLink = FindByFullName (infos, "MyApp.DeepLinkActivity");

		Assert.NotNull (deepLink.ComponentAttribute);
		Assert.Equal ("my.app.DeepLinkActivity", deepLink.ComponentAttribute!.Properties ["Name"]);
		Assert.Equal ("@style/AppTheme", deepLink.ComponentAttribute.Properties ["Theme"]);
		Assert.True ((bool)deepLink.ComponentAttribute.Properties ["Exported"]);
	}
}
