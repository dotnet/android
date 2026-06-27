using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Android.Sdk.TrimmableTypeMap;

using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ManifestGeneratorTests
{
	static readonly XNamespace AndroidNs = "http://schemas.android.com/apk/res/android";
	static readonly XName AttName = AndroidNs + "name";

	ManifestGenerator CreateDefaultGenerator () => new ManifestGenerator {
		PackageName = "com.example.app",
		ApplicationLabel = "My App",
		VersionCode = "1",
		VersionName = "1.0",
		MinSdkVersion = "21",
		TargetSdkVersion = "36",
		RuntimeProviderJavaName = "mono.MonoRuntimeProvider",
	};

	static XDocument ParseTemplate (string xml) => XDocument.Parse (xml);

	static JavaPeerInfo CreatePeer (
		string javaName,
		ComponentInfo? component = null,
		bool isAbstract = false,
		string assemblyName = "TestApp")
	{
		return new JavaPeerInfo {
			JavaName = javaName,
			CompatJniName = javaName,
			ManagedTypeName = javaName.Replace ('/', '.'),
			ManagedTypeNamespace = javaName.Contains ('/') ? javaName.Substring (0, javaName.LastIndexOf ('/')).Replace ('/', '.') : "",
			ManagedTypeShortName = javaName.Contains ('/') ? javaName.Substring (javaName.LastIndexOf ('/') + 1) : javaName,
			AssemblyName = assemblyName,
			IsAbstract = isAbstract,
			ComponentAttribute = component,
		};
	}

	XDocument GenerateAndLoad (
		ManifestGenerator gen,
		IReadOnlyList<JavaPeerInfo>? peers = null,
		AssemblyManifestInfo? assemblyInfo = null,
		XDocument? template = null)
	{
		peers ??= [];
		assemblyInfo ??= new AssemblyManifestInfo ();
		var (doc, _) = gen.Generate (template, peers, assemblyInfo);
		return doc;
	}

	[Fact]
	public void Placeholders_InvalidEntryWithoutValue_WarnsXA1010 ()
	{
		var gen = CreateDefaultGenerator ();
		var warnings = new List<string> ();
		gen.WarnInvalidPlaceholder = warnings.Add;
		gen.ManifestPlaceholders = "ph2=a=b;ph1";
		var template = ParseTemplate ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
			  <uses-sdk />
			  <application android:label="${ph1}" />
			</manifest>
			""");
		GenerateAndLoad (gen, template: template);
		Assert.Single (warnings);
		Assert.Contains ("ph1", warnings [0]);
	}

	[Fact]
	public void Placeholders_AllValid_DoesNotWarn ()
	{
		var gen = CreateDefaultGenerator ();
		var warnings = new List<string> ();
		gen.WarnInvalidPlaceholder = warnings.Add;
		gen.ManifestPlaceholders = "ph1=val1;ph2=val2";
		var template = ParseTemplate ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
			  <uses-sdk />
			  <application android:label="${ph1}" />
			</manifest>
			""");
		var doc = GenerateAndLoad (gen, template: template);
		Assert.Empty (warnings);
		Assert.Equal ("val1", (string?) doc.Root?.Element ("application")?.Attribute (AndroidNs + "label"));
	}

	[Fact]
	public void Package_PlaceholderToken_ReplacedWithResolvedPackageName ()
	{
		// A template package that is a placeholder token (e.g. "${PACKAGENAME}") must be replaced
		// with the resolved $(_AndroidPackage); otherwise manifest validation fails (AllResourcesInClassLibrary).
		var gen = CreateDefaultGenerator ();
		gen.PackageName = "x__PACKAGENAME_.x__PACKAGENAME_";
		var template = ParseTemplate ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="${PACKAGENAME}">
			  <uses-sdk />
			  <application android:label="App" />
			</manifest>
			""");
		var doc = GenerateAndLoad (gen, template: template);
		Assert.Equal ("x__PACKAGENAME_.x__PACKAGENAME_", (string?) doc.Root?.Attribute ("package"));
	}

	[Fact]
	public void Package_ValidExplicitPackage_Preserved ()
	{
		var gen = CreateDefaultGenerator ();
		gen.PackageName = "com.other.app";
		var template = ParseTemplate ("""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
			  <uses-sdk />
			  <application android:label="App" />
			</manifest>
			""");
		var doc = GenerateAndLoad (gen, template: template);
		Assert.Equal ("com.example.app", (string?) doc.Root?.Attribute ("package"));
	}

	[Fact]
	public void Activity_MainLauncher ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MainActivity", new ComponentInfo { 
			Kind = ComponentKind.Activity,
			Properties = new Dictionary<string, object?> { ["MainLauncher"] = true },
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var app = doc.Root?.Element ("application");
		Assert.NotNull (app);
		var activity = app?.Element ("activity");
		Assert.NotNull (activity);

		Assert.Equal ("com.example.app.MainActivity", (string?)activity?.Attribute (AttName));
		Assert.Equal ("true", (string?)activity?.Attribute (AndroidNs + "exported"));

		var filter = activity?.Element ("intent-filter");
		Assert.NotNull (filter);
		Assert.True (filter?.Elements ("action").Any (a => (string?)a.Attribute (AttName) == "android.intent.action.MAIN"));
		Assert.True (filter?.Elements ("category").Any (c => (string?)c.Attribute (AttName) == "android.intent.category.LAUNCHER"));
	}

	[Fact]
	public void Activity_WithProperties ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MyActivity", new ComponentInfo { 
			Kind = ComponentKind.Activity,
			Properties = new Dictionary<string, object?> {
				["Label"] = "My Activity",
				["Icon"] = "@drawable/icon",
				["Theme"] = "@style/MyTheme",
				["LaunchMode"] = 2, // singleTask
				["ScreenOrientation"] = 7, // sensorPortrait
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var activity = doc.Root?.Element ("application")?.Element ("activity");

		Assert.Equal ("My Activity", (string?)activity?.Attribute (AndroidNs + "label"));
		Assert.Equal ("@drawable/icon", (string?)activity?.Attribute (AndroidNs + "icon"));
		Assert.Equal ("@style/MyTheme", (string?)activity?.Attribute (AndroidNs + "theme"));
		Assert.Equal ("singleTask", (string?)activity?.Attribute (AndroidNs + "launchMode"));
		Assert.Equal ("sensorPortrait", (string?)activity?.Attribute (AndroidNs + "screenOrientation"));
	}

	[Fact]
	public void Activity_AttributesAreSortedAlphabetically ()
	{
		// The legacy ManifestDocumentElement sorts attributes alphabetically; the trimmable
		// generator must match so the 'legacy' AndroidManifestMerger path is byte-compatible.
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MyActivity", new ComponentInfo {
			Kind = ComponentKind.Activity,
			Properties = new Dictionary<string, object?> {
				["Theme"] = "@style/MyTheme",
				["Label"] = "My Activity",
				["Exported"] = true,
				["Enabled"] = true,
				["Icon"] = "@drawable/icon",
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var activity = doc.Root?.Element ("application")?.Element ("activity");
		Assert.NotNull (activity);
		var localNames = activity!.Attributes ().Select (a => a.Name.LocalName).ToList ();
		var expected = localNames.OrderBy (n => n, System.StringComparer.OrdinalIgnoreCase).ToList ();
		Assert.Equal (expected, localNames);
		// android:name must appear in its alphabetical position (not forced first).
		Assert.Contains ("enabled", localNames);
		Assert.True (localNames.IndexOf ("enabled") < localNames.IndexOf ("exported"));
		Assert.True (localNames.IndexOf ("label") < localNames.IndexOf ("name"));
		Assert.True (localNames.IndexOf ("name") < localNames.IndexOf ("theme"));
	}

	[Fact]
	public void Activity_IntentFilter ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/ShareActivity", new ComponentInfo { 
			Kind = ComponentKind.Activity,
			IntentFilters = [
				new IntentFilterInfo {
					Actions = ["android.intent.action.SEND"],
					Categories = ["android.intent.category.DEFAULT"],
					Properties = new Dictionary<string, object?> {
						["DataMimeType"] = "text/plain",
					},
				},
			],
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var activity = doc.Root?.Element ("application")?.Element ("activity");

		var filter = activity?.Element ("intent-filter");
		Assert.NotNull (filter);
		Assert.True (filter?.Elements ("action").Any (a => (string?)a.Attribute (AttName) == "android.intent.action.SEND"));
		Assert.True (filter?.Elements ("category").Any (c => (string?)c.Attribute (AttName) == "android.intent.category.DEFAULT"));

		var data = filter?.Element ("data");
		Assert.NotNull (data);
		Assert.Equal ("text/plain", (string?)data?.Attribute (AndroidNs + "mimeType"));
	}

	[Fact]
	public void Activity_IntentFilterPluralDataProperties ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/ShareActivity", new ComponentInfo {
			Kind = ComponentKind.Activity,
			IntentFilters = [
				new IntentFilterInfo {
					Actions = ["action1"],
					Properties = new Dictionary<string, object?> {
						["DataPaths"] = new List<string> { "foo", "bar" },
						["DataPathPatterns"] = new List<string> { "foo*", "bar*" },
						["DataPathPrefixes"] = new List<string> { "foo", "bar" },
						["DataHosts"] = new List<string> { "foo.com", "bar.com" },
						["DataPorts"] = new List<string> { "10000", "20000" },
						["DataSchemes"] = new List<string> { "http", "ftp" },
						["DataMimeTypes"] = new List<string> { "text/html", "text/xml" },
					},
				},
			],
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var filter = doc.Root?.Element ("application")?.Element ("activity")?.Element ("intent-filter");

		Assert.NotNull (filter);
		Assert.Equal (14, filter?.Elements ("data").Count ());
	}

	[Fact]
	public void Activity_IntentFilterSingularDataPropertiesEachGetOwnElement ()
	{
		// Mirrors the IntentFilterData device test: each singular Data* property must
		// produce its own <data> element (matching legacy IntentFilterAttribute.GetData ()).
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/ShareActivity", new ComponentInfo {
			Kind = ComponentKind.Activity,
			IntentFilters = [
				new IntentFilterInfo {
					Actions = ["action1"],
					Properties = new Dictionary<string, object?> {
						["DataPath"] = "foo",
						["DataPathPattern"] = "foo*",
						["DataPathPrefix"] = "foo",
						["Label"] = "testTarget",
					},
				},
			],
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var filter = doc.Root?.Element ("application")?.Element ("activity")?.Element ("intent-filter");

		Assert.NotNull (filter);
		Assert.Equal ("testTarget", (string?)filter?.Attribute (AndroidNs + "label"));
		Assert.Equal (3, filter?.Elements ("data").Count ());
	}

	[Fact]
	public void Activity_MetaData ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MetaActivity", new ComponentInfo { 
			Kind = ComponentKind.Activity,
			MetaData = [
				new MetaDataInfo { Name = "com.example.key", Value = "my_value" },
				new MetaDataInfo { Name = "com.example.res", Resource = "@xml/config" },
			],
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var activity = doc.Root?.Element ("application")?.Element ("activity");

		var metaElements = activity?.Elements ("meta-data").ToList ();
		Assert.Equal (2, metaElements?.Count);

		var meta1 = metaElements?.FirstOrDefault (m => (string?)m.Attribute (AndroidNs + "name") == "com.example.key");
		Assert.NotNull (meta1);
		Assert.Equal ("my_value", (string?)meta1?.Attribute (AndroidNs + "value"));

		var meta2 = metaElements?.FirstOrDefault (m => (string?)m.Attribute (AndroidNs + "name") == "com.example.res");
		Assert.NotNull (meta2);
		Assert.Equal ("@xml/config", (string?)meta2?.Attribute (AndroidNs + "resource"));
	}

	[Fact]
	public void Activity_LayoutAttributeElement ()
	{
		// Mirrors the LayoutAttributeElement device test: a [Layout] attribute on an activity
		// must produce a <layout> child element with the mapped android: attributes.
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MainActivity", new ComponentInfo {
			Kind = ComponentKind.Activity,
			LayoutProperties = new Dictionary<string, object?> {
				["DefaultWidth"] = "500dp",
				["DefaultHeight"] = "600dp",
				["Gravity"] = "center",
				["MinWidth"] = "300dp",
				["MinHeight"] = "400dp",
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var layout = doc.Root?.Element ("application")?.Element ("activity")?.Element ("layout");

		Assert.NotNull (layout);
		Assert.Equal ("500dp", (string?)layout?.Attribute (AndroidNs + "defaultWidth"));
		Assert.Equal ("600dp", (string?)layout?.Attribute (AndroidNs + "defaultHeight"));
		Assert.Equal ("center", (string?)layout?.Attribute (AndroidNs + "gravity"));
		Assert.Equal ("300dp", (string?)layout?.Attribute (AndroidNs + "minWidth"));
		Assert.Equal ("400dp", (string?)layout?.Attribute (AndroidNs + "minHeight"));
	}

	[Fact]
	public void Activity_AllExtendedProperties ()
	{
		// Covers the activity attributes added for AllActivityAttributeProperties: each must be
		// emitted on the <activity> element (the manifestmerger.jar then sorts them alphabetically).
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/TestActivity", new ComponentInfo {
			Kind = ComponentKind.Activity,
			Properties = new Dictionary<string, object?> {
				["AllowEmbedded"] = true,
				["AutoRemoveFromRecents"] = true,
				["Banner"] = "@drawable/icon",
				["ColorMode"] = "hdr",
				["EnableVrMode"] = "foo",
				["LockTaskMode"] = "normal",
				["Logo"] = "@drawable/icon",
				["MaxAspectRatio"] = 1.2f,
				["MaxRecents"] = 1,
				["RecreateOnConfigChanges"] = 0x0001, // ConfigChanges.Mcc
				["RelinquishTaskIdentity"] = true,
				["ResumeWhilePausing"] = true,
				["RotationAnimation"] = 1, // WindowRotationAnimation.Crossfade
				["ShowOnLockScreen"] = true,
				["ShowWhenLocked"] = true,
				["SingleUser"] = true,
				["VisibleToInstantApps"] = true,
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var activity = doc.Root?.Element ("application")?.Element ("activity");
		Assert.NotNull (activity);

		Assert.Equal ("true", (string?)activity?.Attribute (AndroidNs + "allowEmbedded"));
		Assert.Equal ("true", (string?)activity?.Attribute (AndroidNs + "autoRemoveFromRecents"));
		Assert.Equal ("@drawable/icon", (string?)activity?.Attribute (AndroidNs + "banner"));
		Assert.Equal ("hdr", (string?)activity?.Attribute (AndroidNs + "colorMode"));
		Assert.Equal ("foo", (string?)activity?.Attribute (AndroidNs + "enableVrMode"));
		Assert.Equal ("normal", (string?)activity?.Attribute (AndroidNs + "lockTaskMode"));
		Assert.Equal ("@drawable/icon", (string?)activity?.Attribute (AndroidNs + "logo"));
		Assert.Equal ("1.2", (string?)activity?.Attribute (AndroidNs + "maxAspectRatio"));
		Assert.Equal ("1", (string?)activity?.Attribute (AndroidNs + "maxRecents"));
		Assert.Equal ("mcc", (string?)activity?.Attribute (AndroidNs + "recreateOnConfigChanges"));
		Assert.Equal ("true", (string?)activity?.Attribute (AndroidNs + "relinquishTaskIdentity"));
		Assert.Equal ("true", (string?)activity?.Attribute (AndroidNs + "resumeWhilePausing"));
		Assert.Equal ("crossfade", (string?)activity?.Attribute (AndroidNs + "rotationAnimation"));
		Assert.Equal ("true", (string?)activity?.Attribute (AndroidNs + "showOnLockScreen"));
		Assert.Equal ("true", (string?)activity?.Attribute (AndroidNs + "showWhenLocked"));
		Assert.Equal ("true", (string?)activity?.Attribute (AndroidNs + "singleUser"));
		Assert.Equal ("true", (string?)activity?.Attribute (AndroidNs + "visibleToInstantApps"));
	}

	[Fact]
	public void Activity_ParentActivityResolvesToManifestName ()
	{
		// [Activity (ParentActivity = typeof (Parent))] captures the managed type name; the generator
		// must resolve it to the parent's Java/manifest name (here the JCW package differs from the
		// managed namespace).
		var gen = CreateDefaultGenerator ();
		var parent = new JavaPeerInfo {
			JavaName = "com/foo/bar/MainActivity",
			CompatJniName = "com/foo/bar/MainActivity",
			ManagedTypeName = "UnnamedProject.MainActivity",
			ManagedTypeNamespace = "UnnamedProject",
			ManagedTypeShortName = "MainActivity",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo { Kind = ComponentKind.Activity },
		};
		var child = CreatePeer ("com/example/app/ChildActivity", new ComponentInfo {
			Kind = ComponentKind.Activity,
			Properties = new Dictionary<string, object?> {
				["ParentActivity"] = "UnnamedProject.MainActivity",
			},
		});

		var doc = GenerateAndLoad (gen, [parent, child]);
		var childEl = doc.Root?.Element ("application")?.Elements ("activity")
			.FirstOrDefault (a => (string?)a.Attribute (AttName) == "com.example.app.ChildActivity");

		Assert.NotNull (childEl);
		Assert.Equal ("com.foo.bar.MainActivity", (string?)childEl?.Attribute (AndroidNs + "parentActivityName"));
	}

	[Theory]
	[InlineData (ComponentKind.Service, "service")]
	[InlineData (ComponentKind.BroadcastReceiver, "receiver")]
	public void Component_BasicProperties (ComponentKind kind, string elementName)
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MyComponent", new ComponentInfo {
			Kind = kind,
			Properties = new Dictionary<string, object?> {
				["Exported"] = true,
				["Label"] = "My Component",
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var element = doc.Root?.Element ("application")?.Element (elementName);
		Assert.NotNull (element);

		Assert.Equal ("com.example.app.MyComponent", (string?)element?.Attribute (AttName));
		Assert.Equal ("true", (string?)element?.Attribute (AndroidNs + "exported"));
		Assert.Equal ("My Component", (string?)element?.Attribute (AndroidNs + "label"));
	}

	[Fact]
	public void ContentProvider_WithAuthorities ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MyProvider", new ComponentInfo { 
			Kind = ComponentKind.ContentProvider,
			Properties = new Dictionary<string, object?> {
				["Authorities"] = "com.example.app.provider",
				["Exported"] = false,
				["GrantUriPermissions"] = true,
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var provider = doc.Root?.Element ("application")?.Element ("provider");
		Assert.NotNull (provider);

		Assert.Equal ("com.example.app.MyProvider", (string?)provider?.Attribute (AttName));
		Assert.Equal ("com.example.app.provider", (string?)provider?.Attribute (AndroidNs + "authorities"));
		Assert.Equal ("false", (string?)provider?.Attribute (AndroidNs + "exported"));
		Assert.Equal ("true", (string?)provider?.Attribute (AndroidNs + "grantUriPermissions"));
	}

	[Fact]
	public void Application_TypeLevel ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MyApp", new ComponentInfo { 
			Kind = ComponentKind.Application,
			Properties = new Dictionary<string, object?> {
				["Label"] = "Custom App",
				["AllowBackup"] = false,
				["LargeHeap"] = true,
				["DirectBootAware"] = true,
				["NetworkSecurityConfig"] = "@xml/network_security_config",
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var app = doc.Root?.Element ("application");
		Assert.NotNull (app);

		Assert.Equal ("com.example.app.MyApp", (string?)app?.Attribute (AttName));
		Assert.Equal ("false", (string?)app?.Attribute (AndroidNs + "allowBackup"));
		Assert.Equal ("true", (string?)app?.Attribute (AndroidNs + "largeHeap"));
		Assert.Equal ("true", (string?)app?.Attribute (AndroidNs + "directBootAware"));
		Assert.Equal ("@xml/network_security_config", (string?)app?.Attribute (AndroidNs + "networkSecurityConfig"));
		Assert.Equal ("true", (string?)app?.Attribute (AndroidNs + "extractNativeLibs"));

		var provider = app?.Element ("provider");
		Assert.NotNull (provider);
		Assert.Equal ("true", (string?)provider?.Attribute (AndroidNs + "directBootAware"));
	}

	[Fact]
	public void Instrumentation_GoesToManifest ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MyInstrumentation", new ComponentInfo { 
			Kind = ComponentKind.Instrumentation,
			Properties = new Dictionary<string, object?> {
				["Label"] = "My Test",
				["TargetPackage"] = "com.example.target",
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);

		// Instrumentation should be under <manifest>, not <application>
		var instrumentation = doc.Root?.Element ("instrumentation");
		Assert.NotNull (instrumentation);

		Assert.Equal ("com.example.app.MyInstrumentation", (string?)instrumentation?.Attribute (AttName));
		Assert.Equal ("My Test", (string?)instrumentation?.Attribute (AndroidNs + "label"));
		Assert.Equal ("com.example.target", (string?)instrumentation?.Attribute (AndroidNs + "targetPackage"));

		// Should NOT be inside <application>
		var appInstrumentation = doc.Root?.Element ("application")?.Element ("instrumentation");
		Assert.Null (appInstrumentation);
	}

	[Fact]
	public void Instrumentation_DefaultsTargetPackage ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/MyInstrumentation", new ComponentInfo {
			Kind = ComponentKind.Instrumentation,
			Properties = new Dictionary<string, object?> {
				["Label"] = "My Test",
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);

		var instrumentation = doc.Root?.Element ("instrumentation");
		Assert.NotNull (instrumentation);

		// targetPackage should default to the app's PackageName
		Assert.Equal ("com.example.app", (string?)instrumentation?.Attribute (AndroidNs + "targetPackage"));
	}

	[Fact]
	public void CompatNames_RewrittenToCrc ()
	{
		var gen = CreateDefaultGenerator ();

		// Template uses compat names
		var template = ParseTemplate ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
				<application android:name="com.example.app.MyApp">
					<activity android:name="com.example.app.MainActivity" />
				</application>
			</manifest>
			""");

		// Peer has CRC JavaName but compat CompatJniName
		var appPeer = new JavaPeerInfo {
			JavaName = "crc64abc123/MyApp",
			CompatJniName = "com/example/app/MyApp",
			ManagedTypeName = "Com.Example.App.MyApp",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MyApp",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Application,
				Properties = new Dictionary<string, object?> (),
			},
		};
		var activityPeer = new JavaPeerInfo {
			JavaName = "crc64def456/MainActivity",
			CompatJniName = "com/example/app/MainActivity",
			ManagedTypeName = "Com.Example.App.MainActivity",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MainActivity",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Activity,
				Properties = new Dictionary<string, object?> (),
			},
		};

		var doc = GenerateAndLoad (gen, [appPeer, activityPeer], template: template);

		var app = doc.Root?.Element ("application");
		Assert.NotNull (app);
		Assert.Equal ("crc64abc123.MyApp", (string?)app?.Attribute (AttName));

		var activity = app?.Element ("activity");
		Assert.NotNull (activity);
		Assert.Equal ("crc64def456.MainActivity", (string?)activity?.Attribute (AttName));
	}

	[Fact]
	public void CompatNames_RewrittenToCrc_RelativeDotForm ()
	{
		var gen = CreateDefaultGenerator ();

		// Template uses the ".Type" relative form that Android resolves against the
		// manifest package. RewriteCompatNames must resolve it before the compat lookup.
		var template = ParseTemplate ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
				<application android:name=".MyApp">
					<activity android:name=".MainActivity" />
				</application>
			</manifest>
			""");

		var appPeer = new JavaPeerInfo {
			JavaName = "crc64abc123/MyApp",
			CompatJniName = "com/example/app/MyApp",
			ManagedTypeName = "Com.Example.App.MyApp",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MyApp",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Application,
				Properties = new Dictionary<string, object?> (),
			},
		};
		var activityPeer = new JavaPeerInfo {
			JavaName = "crc64def456/MainActivity",
			CompatJniName = "com/example/app/MainActivity",
			ManagedTypeName = "Com.Example.App.MainActivity",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MainActivity",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Activity,
				Properties = new Dictionary<string, object?> (),
			},
		};

		var doc = GenerateAndLoad (gen, [appPeer, activityPeer], template: template);

		var app = doc.Root?.Element ("application");
		Assert.NotNull (app);
		Assert.Equal ("crc64abc123.MyApp", (string?)app?.Attribute (AttName));

		var activity = app?.Element ("activity");
		Assert.NotNull (activity);
		Assert.Equal ("crc64def456.MainActivity", (string?)activity?.Attribute (AttName));
	}

	[Fact]
	public void CompatNames_RewrittenToCrc_UsesManifestPackageAttribute ()
	{
		var gen = CreateDefaultGenerator ();
		gen.PackageName = "com.other.app";

		var template = ParseTemplate ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
				<application android:name=".MyApp">
					<activity android:name=".MainActivity" />
				</application>
			</manifest>
			""");

		var appPeer = new JavaPeerInfo {
			JavaName = "crc64abc123/MyApp",
			CompatJniName = "com/example/app/MyApp",
			ManagedTypeName = "Com.Example.App.MyApp",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MyApp",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Application,
				Properties = new Dictionary<string, object?> (),
			},
		};
		var activityPeer = new JavaPeerInfo {
			JavaName = "crc64def456/MainActivity",
			CompatJniName = "com/example/app/MainActivity",
			ManagedTypeName = "Com.Example.App.MainActivity",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MainActivity",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Activity,
				Properties = new Dictionary<string, object?> (),
			},
		};

		var doc = GenerateAndLoad (gen, [appPeer, activityPeer], template: template);

		var app = doc.Root?.Element ("application");
		Assert.NotNull (app);
		Assert.Equal ("crc64abc123.MyApp", (string?)app?.Attribute (AttName));

		var activity = app?.Element ("activity");
		Assert.NotNull (activity);
		Assert.Equal ("crc64def456.MainActivity", (string?)activity?.Attribute (AttName));
	}

	[Fact]
	public void CompatNames_RewrittenToCrc_UnqualifiedForm ()
	{
		var gen = CreateDefaultGenerator ();

		// Template uses the bare "Type" form (no dot) that Android also resolves
		// against the manifest package.
		var template = ParseTemplate ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
				<application android:name="MyApp">
					<activity android:name="MainActivity" />
				</application>
			</manifest>
			""");

		var appPeer = new JavaPeerInfo {
			JavaName = "crc64abc123/MyApp",
			CompatJniName = "com/example/app/MyApp",
			ManagedTypeName = "Com.Example.App.MyApp",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MyApp",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Application,
				Properties = new Dictionary<string, object?> (),
			},
		};
		var activityPeer = new JavaPeerInfo {
			JavaName = "crc64def456/MainActivity",
			CompatJniName = "com/example/app/MainActivity",
			ManagedTypeName = "Com.Example.App.MainActivity",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MainActivity",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Activity,
				Properties = new Dictionary<string, object?> (),
			},
		};

		var doc = GenerateAndLoad (gen, [appPeer, activityPeer], template: template);

		var app = doc.Root?.Element ("application");
		Assert.NotNull (app);
		Assert.Equal ("crc64abc123.MyApp", (string?)app?.Attribute (AttName));

		var activity = app?.Element ("activity");
		Assert.NotNull (activity);
		Assert.Equal ("crc64def456.MainActivity", (string?)activity?.Attribute (AttName));
	}

	[Fact]
	public void CompatNames_RelativeForm_NotDuplicated ()
	{
		// Regression: when a template uses a relative name (".MainActivity"), the
		// rewrite must resolve it to the fully-qualified compat name before looking
		// up the CRC mapping. If the relative name slipped through unchanged, the
		// duplicate-detection in ManifestGenerator would miss it and emit a second
		// CRC-named <activity> alongside the existing relative-named entry.
		var gen = CreateDefaultGenerator ();
		var template = ParseTemplate ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
				<application>
					<activity android:name=".MainActivity" android:label="Existing" />
				</application>
			</manifest>
			""");

		var peer = new JavaPeerInfo {
			JavaName = "crc64def456/MainActivity",
			CompatJniName = "com/example/app/MainActivity",
			ManagedTypeName = "Com.Example.App.MainActivity",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MainActivity",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Activity,
				Properties = new Dictionary<string, object?> { ["Label"] = "New Label" },
			},
		};

		var doc = GenerateAndLoad (gen, [peer], template: template);
		var activities = doc.Root?.Element ("application")?.Elements ("activity").ToList ();

		Assert.NotNull (activities);
		Assert.Single (activities!);
		Assert.Equal ("crc64def456.MainActivity", (string?)activities [0].Attribute (AttName));
		// Existing android:label from the template is preserved (duplicate detection worked).
		Assert.Equal ("Existing", (string?)activities [0].Attribute (AndroidNs + "label"));
	}

	[Fact]
	public void CompatNames_MetadataName_NotRewritten_AndDoesNotSuppressActivity ()
	{
		var gen = CreateDefaultGenerator ();
		var template = ParseTemplate ("""
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
				<application>
					<meta-data android:name=".MainActivity" android:value="keep-me" />
				</application>
			</manifest>
			""");

		var peer = new JavaPeerInfo {
			JavaName = "crc64def456/MainActivity",
			CompatJniName = "com/example/app/MainActivity",
			ManagedTypeName = "Com.Example.App.MainActivity",
			ManagedTypeNamespace = "Com.Example.App",
			ManagedTypeShortName = "MainActivity",
			AssemblyName = "TestApp",
			ComponentAttribute = new ComponentInfo {
				Kind = ComponentKind.Activity,
				Properties = new Dictionary<string, object?> (),
			},
		};

		var doc = GenerateAndLoad (gen, [peer], template: template);
		var app = doc.Root?.Element ("application");
		Assert.NotNull (app);

		var metadata = app?.Element ("meta-data");
		Assert.NotNull (metadata);
		Assert.Equal (".MainActivity", (string?)metadata?.Attribute (AttName));

		var activities = app?.Elements ("activity").ToList ();
		Assert.NotNull (activities);
		Assert.Single (activities!);
		Assert.Equal ("crc64def456.MainActivity", (string?)activities [0].Attribute (AttName));
	}

	[Fact]
	public void RuntimeProvider_Added ()
	{
		var gen = CreateDefaultGenerator ();
		var doc = GenerateAndLoad (gen);
		var app = doc.Root?.Element ("application");

		var providers = app?.Elements ("provider").ToList ();
		Assert.True (providers?.Count > 0);

		var runtimeProvider = providers?.FirstOrDefault (p =>
			((string?)p.Attribute (AndroidNs + "name"))?.Contains ("MonoRuntimeProvider") == true);
		Assert.NotNull (runtimeProvider);

		var authorities = (string?)runtimeProvider?.Attribute (AndroidNs + "authorities");
		Assert.True (authorities?.Contains ("com.example.app") == true, "authorities should contain package name");
		Assert.True (authorities?.Contains ("__mono_init__") == true, "authorities should contain __mono_init__");
		Assert.Equal ("false", (string?)runtimeProvider?.Attribute (AndroidNs + "exported"));
	}

	[Fact]
	public void TemplateManifest_Preserved ()
	{
		var gen = CreateDefaultGenerator ();
		var template = ParseTemplate (
			"""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
			  <application android:allowBackup="false" android:icon="@mipmap/ic_launcher">
			  </application>
			</manifest>
			""");

		var doc = GenerateAndLoad (gen, template: template);
		var app = doc.Root?.Element ("application");

		Assert.Equal ("false", (string?)app?.Attribute (AndroidNs + "allowBackup"));
		Assert.Equal ("@mipmap/ic_launcher", (string?)app?.Attribute (AndroidNs + "icon"));
	}

	[Theory]
	[InlineData ("", "", "1", "1.0")]
	[InlineData ("42", "2.5", "42", "2.5")]
	public void VersionDefaults (string versionCode, string versionName, string expectedCode, string expectedName)
	{
		var gen = CreateDefaultGenerator ();
		gen.VersionCode = versionCode;
		gen.VersionName = versionName;

		var doc = GenerateAndLoad (gen);
		Assert.Equal (expectedCode, (string?)doc.Root?.Attribute (AndroidNs + "versionCode"));
		Assert.Equal (expectedName, (string?)doc.Root?.Attribute (AndroidNs + "versionName"));
	}

	[Fact]
	public void UsesSdk_Added ()
	{
		var gen = CreateDefaultGenerator ();
		gen.MinSdkVersion = "24";
		gen.TargetSdkVersion = "34";

		var doc = GenerateAndLoad (gen);
		var usesSdk = doc.Root?.Element ("uses-sdk");
		Assert.NotNull (usesSdk);

		Assert.Equal ("24", (string?)usesSdk?.Attribute (AndroidNs + "minSdkVersion"));
		Assert.Equal ("34", (string?)usesSdk?.Attribute (AndroidNs + "targetSdkVersion"));
	}

	[Fact]
	public void UsesSdk_MissingMinSdkVersion_SetFromConfiguredMinSdk ()
	{
		var gen = CreateDefaultGenerator ();
		gen.MinSdkVersion = "24";
		var template = ParseTemplate (
			"""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
			  <uses-sdk />
			  <application />
			</manifest>
			""");

		var doc = GenerateAndLoad (gen, template: template);
		var usesSdk = doc.Root?.Element ("uses-sdk");
		Assert.NotNull (usesSdk);

		Assert.Equal ("24", (string?)usesSdk?.Attribute (AndroidNs + "minSdkVersion"));
	}

	[Theory]
	[InlineData (true, false, false, "debuggable", "true")]
	[InlineData (false, true, false, "debuggable", "true")]
	[InlineData (false, false, true, "extractNativeLibs", "true")]
	public void ApplicationFlags (bool debug, bool forceDebuggable, bool forceExtractNativeLibs, string attrName, string expected)
	{
		var gen = CreateDefaultGenerator ();
		gen.Debug = debug;
		gen.ForceDebuggable = forceDebuggable;
		gen.ForceExtractNativeLibs = forceExtractNativeLibs;

		var doc = GenerateAndLoad (gen);
		var app = doc.Root?.Element ("application");
		Assert.Equal (expected, (string?)app?.Attribute (AndroidNs + attrName));
	}

	[Fact]
	public void InternetPermission_WhenDebug ()
	{
		var gen = CreateDefaultGenerator ();
		gen.Debug = true;

		var doc = GenerateAndLoad (gen);
		var internetPerm = doc.Root?.Elements ("uses-permission")
			.FirstOrDefault (p => (string?)p.Attribute (AndroidNs + "name") == "android.permission.INTERNET");
		Assert.NotNull (internetPerm);
	}

	[Fact]
	public void ManifestPlaceholders_Replaced ()
	{
		var gen = CreateDefaultGenerator ();
		gen.ManifestPlaceholders = "myAuthority=com.example.auth;myKey=12345";

		var template = ParseTemplate (
			"""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
			  <application android:label="Test">
			    <provider android:name="com.example.MyProvider" android:authorities="${myAuthority}" />
			    <meta-data android:name="api_key" android:value="${myKey}" />
			  </application>
			</manifest>
			""");

		var doc = GenerateAndLoad (gen, template: template);
		var provider = doc.Root?.Element ("application")?.Elements ("provider")
			.FirstOrDefault (p => (string?)p.Attribute (AndroidNs + "name") == "com.example.MyProvider");
		Assert.Equal ("com.example.auth", (string?)provider?.Attribute (AndroidNs + "authorities"));

		var meta = doc.Root?.Element ("application")?.Elements ("meta-data")
			.FirstOrDefault (m => (string?)m.Attribute (AndroidNs + "name") == "api_key");
		Assert.Equal ("12345", (string?)meta?.Attribute (AndroidNs + "value"));
	}

	[Fact]
	public void ApplicationIdPlaceholder_ReplacedWithPackageName ()
	{
		// ${applicationId} is a built-in placeholder (not a user key=value entry) that resolves
		// to the application package name. It appears in merged library-manifest content such as
		// a <permission> name or a <provider> authority (see MergeLibraryManifest).
		var gen = CreateDefaultGenerator ();

		var template = ParseTemplate (
			"""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
			  <permission android:name="${applicationId}.permission.C2D_MESSAGE" android:protectionLevel="signature" />
			  <application android:label="Test">
			    <provider android:name="com.example.FacebookInitProvider" android:authorities="${applicationId}.FacebookInitProvider" android:exported="false" />
			  </application>
			</manifest>
			""");

		var doc = GenerateAndLoad (gen, template: template);

		var permission = doc.Root?.Elements ("permission").FirstOrDefault ();
		Assert.Equal ("com.example.app.permission.C2D_MESSAGE", (string?)permission?.Attribute (AndroidNs + "name"));

		var provider = doc.Root?.Element ("application")?.Elements ("provider")
			.FirstOrDefault (p => (string?)p.Attribute (AndroidNs + "name") == "com.example.FacebookInitProvider");
		Assert.Equal ("com.example.app.FacebookInitProvider", (string?)provider?.Attribute (AndroidNs + "authorities"));
	}

	[Fact]
	public void LibraryManifest_MergedAndRelativeNamesQualified ()
	{
		// Mirrors MergeLibraryManifest: a library (.aar) manifest's top-level elements are merged
		// into the app manifest, relative component names ('.Foo') are qualified with the library's
		// own package, and ${applicationId} resolves to the application package.
		var gen = CreateDefaultGenerator ();
		var libManifest = Path.GetTempFileName ();
		try {
			File.WriteAllText (libManifest,
				"""
				<?xml version="1.0"?>
				<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.lib.test">
				  <permission android:name="${applicationId}.permission.C2D_MESSAGE" android:protectionLevel="signature" />
				  <application>
				    <provider android:name=".internal.LibProvider" android:authorities="${applicationId}.LibProvider" android:exported="false" />
				  </application>
				</manifest>
				""");
			gen.LibraryManifests = [libManifest];

			var doc = GenerateAndLoad (gen);

			var permission = doc.Root?.Elements ("permission").FirstOrDefault ();
			Assert.Equal ("com.example.app.permission.C2D_MESSAGE", (string?)permission?.Attribute (AndroidNs + "name"));

			var provider = doc.Root?.Element ("application")?.Elements ("provider")
				.FirstOrDefault (p => (string?)p.Attribute (AndroidNs + "authorities") == "com.example.app.LibProvider");
			Assert.NotNull (provider);
			// Relative name qualified with the library's own package, not the app package.
			Assert.Equal ("com.lib.test.internal.LibProvider", (string?)provider!.Attribute (AndroidNs + "name"));
		} finally {
			File.Delete (libManifest);
		}
	}

	[Fact]
	public void ApplicationJavaClass_Set ()
	{
		var gen = CreateDefaultGenerator ();
		gen.ApplicationJavaClass = "com.example.app.CustomApplication";

		var doc = GenerateAndLoad (gen);
		var app = doc.Root?.Element ("application");
		Assert.Equal ("com.example.app.CustomApplication", (string?)app?.Attribute (AttName));
	}

	[Fact]
	public void AbstractTypes_Skipped ()
	{
		var gen = CreateDefaultGenerator ();
		var peer = CreatePeer ("com/example/app/AbstractActivity", new ComponentInfo { 
			Kind = ComponentKind.Activity,
			Properties = new Dictionary<string, object?> { ["Label"] = "Abstract" },
		}, isAbstract: true);

		var doc = GenerateAndLoad (gen, [peer]);
		var activity = doc.Root?.Element ("application")?.Element ("activity");
		Assert.Null (activity);
	}

	[Fact]
	public void ExistingType_NotDuplicated ()
	{
		var gen = CreateDefaultGenerator ();
		var template = ParseTemplate (
			"""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
			  <application android:label="Test">
			    <activity android:name="com.example.app.ExistingActivity" android:label="Existing" />
			  </application>
			</manifest>
			""");

		var peer = CreatePeer ("com/example/app/ExistingActivity", new ComponentInfo { 
			Kind = ComponentKind.Activity,
			Properties = new Dictionary<string, object?> { ["Label"] = "New Label" },
		});

		var doc = GenerateAndLoad (gen, [peer], template: template);
		var activities = doc.Root?.Element ("application")?.Elements ("activity")
			.Where (a => (string?)a.Attribute (AttName) == "com.example.app.ExistingActivity")
			.ToList ();

		Assert.Equal (1, activities?.Count);
		// Original label preserved
		Assert.Equal ("Existing", (string?)activities? [0].Attribute (AndroidNs + "label"));
	}

	[Fact]
	public void AssemblyLevel_UsesPermission ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.UsesPermissions.Add (new UsesPermissionInfo { Name = "android.permission.CAMERA" });

		var doc = GenerateAndLoad (gen, assemblyInfo: info);
		var perm = doc.Root?.Elements ("uses-permission")
			.FirstOrDefault (p => (string?)p.Attribute (AttName) == "android.permission.CAMERA");
		Assert.NotNull (perm);
	}

	[Fact]
	public void AssemblyLevel_UsesFeature ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.UsesFeatures.Add (new UsesFeatureInfo { Name = "android.hardware.camera", Required = false });

		var doc = GenerateAndLoad (gen, assemblyInfo: info);
		var feature = doc.Root?.Elements ("uses-feature")
			.FirstOrDefault (f => (string?)f.Attribute (AttName) == "android.hardware.camera");
		Assert.NotNull (feature);
		Assert.Equal ("false", (string?)feature?.Attribute (AndroidNs + "required"));
	}

	[Fact]
	public void AssemblyLevel_UsesLibrary ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.UsesLibraries.Add (new UsesLibraryInfo { Name = "org.apache.http.legacy", Required = false });

		var doc = GenerateAndLoad (gen, assemblyInfo: info);
		var lib = doc.Root?.Element ("application")?.Elements ("uses-library")
			.FirstOrDefault (l => (string?)l.Attribute (AttName) == "org.apache.http.legacy");
		Assert.NotNull (lib);
		Assert.Equal ("false", (string?)lib?.Attribute (AndroidNs + "required"));
	}

	[Fact]
	public void AssemblyLevel_Permission ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.Permissions.Add (new PermissionInfo {
			Name = "com.example.MY_PERMISSION",
			Properties = new Dictionary<string, object?> {
				["Label"] = "My Permission",
				["Description"] = "A custom permission",
			},
		});

		var doc = GenerateAndLoad (gen, assemblyInfo: info);
		var perm = doc.Root?.Elements ("permission")
			.FirstOrDefault (p => (string?)p.Attribute (AttName) == "com.example.MY_PERMISSION");
		Assert.NotNull (perm);
		Assert.Equal ("My Permission", (string?)perm?.Attribute (AndroidNs + "label"));
		Assert.Equal ("A custom permission", (string?)perm?.Attribute (AndroidNs + "description"));
	}

	[Fact]
	public void AssemblyLevel_MetaData ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.MetaData.Add (new MetaDataInfo { Name = "com.google.android.gms.version", Value = "12345" });

		var doc = GenerateAndLoad (gen, assemblyInfo: info);
		var meta = doc.Root?.Element ("application")?.Elements ("meta-data")
			.FirstOrDefault (m => (string?)m.Attribute (AndroidNs + "name") == "com.google.android.gms.version");
		Assert.NotNull (meta);
		Assert.Equal ("12345", (string?)meta?.Attribute (AndroidNs + "value"));
	}

	[Fact]
	public void AssemblyLevel_Application ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo {
			ApplicationProperties = new Dictionary<string, object?> {
				["Theme"] = "@style/AppTheme",
				["SupportsRtl"] = true,
			},
		};

		var doc = GenerateAndLoad (gen, assemblyInfo: info);
		var app = doc.Root?.Element ("application");
		Assert.Equal ("@style/AppTheme", (string?)app?.Attribute (AndroidNs + "theme"));
		Assert.Equal ("true", (string?)app?.Attribute (AndroidNs + "supportsRtl"));
	}

	[Fact]
	public void AssemblyLevel_Deduplication ()
	{
		var gen = CreateDefaultGenerator ();
		var template = ParseTemplate (
			"""
			<?xml version="1.0" encoding="utf-8"?>
			<manifest xmlns:android="http://schemas.android.com/apk/res/android" package="com.example.app">
			  <uses-permission android:name="android.permission.CAMERA" />
			  <application android:label="Test">
			    <uses-library android:name="org.apache.http.legacy" android:required="true" />
			    <meta-data android:name="existing.key" android:value="existing" />
			  </application>
			</manifest>
			""");

		var info = new AssemblyManifestInfo ();
		info.UsesPermissions.Add (new UsesPermissionInfo { Name = "android.permission.CAMERA" });
		info.UsesLibraries.Add (new UsesLibraryInfo { Name = "org.apache.http.legacy" });
		info.MetaData.Add (new MetaDataInfo { Name = "existing.key", Value = "new_value" });

		var doc = GenerateAndLoad (gen, assemblyInfo: info, template: template);

		var cameraPerms = doc.Root?.Elements ("uses-permission")
			.Where (p => (string?)p.Attribute (AttName) == "android.permission.CAMERA")
			.ToList ();
		Assert.Equal (1, cameraPerms?.Count);

		var libs = doc.Root?.Element ("application")?.Elements ("uses-library")
			.Where (l => (string?)l.Attribute (AttName) == "org.apache.http.legacy")
			.ToList ();
		Assert.Equal (1, libs?.Count);

		var metas = doc.Root?.Element ("application")?.Elements ("meta-data")
			.Where (m => (string?)m.Attribute (AndroidNs + "name") == "existing.key")
			.ToList ();
		Assert.Equal (1, metas?.Count);
	}

	[Fact]
	public void ConfigChanges_EnumConversion ()
	{
		var gen = CreateDefaultGenerator ();
		// orientation (0x0080) | keyboardHidden (0x0020) | screenSize (0x0400)
		int configChanges = 0x0080 | 0x0020 | 0x0400;
		var peer = CreatePeer ("com/example/app/ConfigActivity", new ComponentInfo { 
			Kind = ComponentKind.Activity,
			Properties = new Dictionary<string, object?> {
				["ConfigurationChanges"] = configChanges,
			},
		});

		var doc = GenerateAndLoad (gen, [peer]);
		var activity = doc.Root?.Element ("application")?.Element ("activity");

		var configValue = (string?)activity?.Attribute (AndroidNs + "configChanges");

		// The value should be pipe-separated and contain all three flags
		var parts = configValue?.Split ('|') ?? [];
		Assert.True (parts.Contains ("orientation"), "configChanges should contain 'orientation'");
		Assert.True (parts.Contains ("keyboardHidden"), "configChanges should contain 'keyboardHidden'");
		Assert.True (parts.Contains ("screenSize"), "configChanges should contain 'screenSize'");
		Assert.Equal (3, parts.Length);
	}

	[Fact]
	public void AssemblyLevel_SupportsGLTexture ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.SupportsGLTextures.Add (new SupportsGLTextureInfo { Name = "GL_OES_compressed_ETC1_RGB8_texture" });

		var doc = GenerateAndLoad (gen, assemblyInfo: info);
		var element = doc.Root?.Elements ("supports-gl-texture")
			.FirstOrDefault (e => (string?)e.Attribute (AttName) == "GL_OES_compressed_ETC1_RGB8_texture");
		Assert.NotNull (element);
	}

	[Fact]
	public void AssemblyLevel_UsesPermissionFlags ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.UsesPermissions.Add (new UsesPermissionInfo {
			Name = "android.permission.POST_NOTIFICATIONS",
			UsesPermissionFlags = "neverForLocation",
		});

		var doc = GenerateAndLoad (gen, assemblyInfo: info);
		var perm = doc.Root?.Elements ("uses-permission")
			.FirstOrDefault (e => (string?)e.Attribute (AttName) == "android.permission.POST_NOTIFICATIONS");
		Assert.NotNull (perm);
		Assert.Equal ("neverForLocation", (string?)perm?.Attribute (AndroidNs + "usesPermissionFlags"));
	}

	[Fact]
	public void AssemblyLevel_PermissionRoundIcon ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.Permissions.Add (new PermissionInfo {
			Name = "com.example.MY_PERMISSION",
			Properties = new Dictionary<string, object?> {
				["RoundIcon"] = "@mipmap/ic_launcher_round",
			},
		});

		var doc = GenerateAndLoad (gen, assemblyInfo: info);
		var perm = doc.Root?.Elements ("permission")
			.FirstOrDefault (e => (string?)e.Attribute (AttName) == "com.example.MY_PERMISSION");
		Assert.NotNull (perm);
		Assert.Equal ("@mipmap/ic_launcher_round", (string?)perm?.Attribute (AndroidNs + "roundIcon"));
	}

	[Fact]
	public void AssemblyLevel_ApplicationBackupAgent ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.ApplicationProperties = new Dictionary<string, object?> {
			["BackupAgent"] = "MyApp.MyBackupAgent",
		};
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "com/example/app/MyBackupAgent",
				CompatJniName = "com/example/app/MyBackupAgent",
				ManagedTypeName = "MyApp.MyBackupAgent",
				ManagedTypeNamespace = "MyApp",
				ManagedTypeShortName = "MyBackupAgent",
				AssemblyName = "TestApp",
			},
		};

		var doc = GenerateAndLoad (gen, peers: peers, assemblyInfo: info);
		var app = doc.Root?.Element ("application");
		Assert.NotNull (app);
		Assert.Equal ("com.example.app.MyBackupAgent", (string?)app?.Attribute (AndroidNs + "backupAgent"));
	}

	[Fact]
	public void AssemblyLevel_ApplicationManageSpaceActivity ()
	{
		var gen = CreateDefaultGenerator ();
		var info = new AssemblyManifestInfo ();
		info.ApplicationProperties = new Dictionary<string, object?> {
			["ManageSpaceActivity"] = "MyApp.ManageActivity",
		};
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				JavaName = "com/example/app/ManageActivity",
				CompatJniName = "com/example/app/ManageActivity",
				ManagedTypeName = "MyApp.ManageActivity",
				ManagedTypeNamespace = "MyApp",
				ManagedTypeShortName = "ManageActivity",
				AssemblyName = "TestApp",
			},
		};

		var doc = GenerateAndLoad (gen, peers: peers, assemblyInfo: info);
		var app = doc.Root?.Element ("application");
		Assert.NotNull (app);
		Assert.Equal ("com.example.app.ManageActivity", (string?)app?.Attribute (AndroidNs + "manageSpaceActivity"));
	}
}
