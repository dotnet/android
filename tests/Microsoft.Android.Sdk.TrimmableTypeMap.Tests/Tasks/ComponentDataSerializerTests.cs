using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for ComponentDataSerializer: verifies round-trip serialization
/// of component data between per-assembly scanning and manifest generation.
/// </summary>
public class ComponentDataSerializerTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (ComponentDataSerializerTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	[Fact]
	public void SerializeAndDeserialize_RoundTrips ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);

			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			// Should have at least the component types
			Assert.NotEmpty (deserialized);

			var kinds = deserialized.Select (d => d.ComponentKind).Distinct ().ToHashSet ();
			Assert.Contains (ManifestComponentKind.Activity, kinds);
			Assert.Contains (ManifestComponentKind.Service, kinds);
			Assert.Contains (ManifestComponentKind.BroadcastReceiver, kinds);
			Assert.Contains (ManifestComponentKind.ContentProvider, kinds);
			Assert.Contains (ManifestComponentKind.Application, kinds);
			Assert.Contains (ManifestComponentKind.Instrumentation, kinds);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_PreservesComponentAttribute ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			var activity = deserialized.FirstOrDefault (d => d.FullName == "MyApp.MainActivity");
			Assert.NotNull (activity);
			Assert.NotNull (activity!.ComponentAttribute);
			Assert.True ((bool) activity.ComponentAttribute!.Properties ["MainLauncher"]);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_PreservesIntentFilters ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			var deepLink = deserialized.FirstOrDefault (d => d.FullName == "MyApp.DeepLinkActivity");
			Assert.NotNull (deepLink);
			Assert.Equal (2, deepLink!.IntentFilters.Count);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_PreservesMetaData ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			var deepLink = deserialized.FirstOrDefault (d => d.FullName == "MyApp.DeepLinkActivity");
			Assert.NotNull (deepLink);
			Assert.Equal (2, deepLink!.MetaDataEntries.Count);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_PreservesLayoutAttribute ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			var deepLink = deserialized.FirstOrDefault (d => d.FullName == "MyApp.DeepLinkActivity");
			Assert.NotNull (deepLink);
			Assert.NotNull (deepLink!.LayoutAttribute);
			Assert.Equal ("500dp", deepLink.LayoutAttribute!.Properties ["DefaultWidth"]);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_PreservesPropertyAttributes ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			var deepLink = deserialized.FirstOrDefault (d => d.FullName == "MyApp.DeepLinkActivity");
			Assert.NotNull (deepLink);
			Assert.Single (deepLink!.PropertyAttributes);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_PreservesGrantUriPermissions ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			var provider = deserialized.FirstOrDefault (d => d.FullName == "MyApp.MyProvider");
			Assert.NotNull (provider);
			Assert.Equal (2, provider!.GrantUriPermissions.Count);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_PreservesTypeReferences ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			var app = deserialized.FirstOrDefault (d => d.FullName == "MyApp.MyApplication");
			Assert.NotNull (app);
			Assert.NotNull (app!.ComponentAttribute);
			Assert.True (app.ComponentAttribute!.Properties.ContainsKey ("BackupAgent"));
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_StringValues ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				ManagedTypeName = "Test.Type1",
				JavaName = "test/Type1",
				AssemblyName = "TestAssembly",
				ComponentData = new ComponentData {
					ComponentKind = ManifestComponentKind.Activity,
					ComponentAttribute = new ComponentAttributeInfo {
						AttributeType = "Android.App.ActivityAttribute",
						Properties = new Dictionary<string, object> {
							["Label"] = "Hello World",
							["Theme"] = "@style/Theme.App",
						},
						ConstructorArguments = new List<object> (),
					},
				},
			},
		};

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			Assert.Single (deserialized);
			var type = deserialized [0];
			Assert.Equal ("Hello World", type.ComponentAttribute!.Properties ["Label"]);
			Assert.Equal ("@style/Theme.App", type.ComponentAttribute.Properties ["Theme"]);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_BooleanValues ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				ManagedTypeName = "Test.Type1",
				JavaName = "test/Type1",
				AssemblyName = "TestAssembly",
				ComponentData = new ComponentData {
					ComponentKind = ManifestComponentKind.Activity,
					ComponentAttribute = new ComponentAttributeInfo {
						AttributeType = "Android.App.ActivityAttribute",
						Properties = new Dictionary<string, object> {
							["MainLauncher"] = true,
							["Exported"] = false,
						},
						ConstructorArguments = new List<object> (),
					},
				},
			},
		};

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			Assert.Single (deserialized);
			var type = deserialized [0];
			Assert.True ((bool) type.ComponentAttribute!.Properties ["MainLauncher"]);
			Assert.False ((bool) type.ComponentAttribute.Properties ["Exported"]);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_IntValues ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				ManagedTypeName = "Test.Type1",
				JavaName = "test/Type1",
				AssemblyName = "TestAssembly",
				ComponentData = new ComponentData {
					ComponentKind = ManifestComponentKind.Service,
					ComponentAttribute = new ComponentAttributeInfo {
						AttributeType = "Android.App.ServiceAttribute",
						Properties = new Dictionary<string, object> {
							["ForegroundServiceType"] = 42,
						},
						ConstructorArguments = new List<object> (),
					},
				},
			},
		};

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			Assert.Single (deserialized);
			var type = deserialized [0];
			Assert.Equal (42, type.ComponentAttribute!.Properties ["ForegroundServiceType"]);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_ArrayValues ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				ManagedTypeName = "Test.Provider1",
				JavaName = "test/Provider1",
				AssemblyName = "TestAssembly",
				ComponentData = new ComponentData {
					ComponentKind = ManifestComponentKind.ContentProvider,
					ComponentAttribute = new ComponentAttributeInfo {
						AttributeType = "Android.Content.ContentProviderAttribute",
						Properties = new Dictionary<string, object> (),
						ConstructorArguments = new List<object> {
							new string [] { "auth1", "auth2" },
						},
					},
				},
			},
		};

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			Assert.Single (deserialized);
			var type = deserialized [0];
			var ctorArg = type.ComponentAttribute!.ConstructorArguments [0];
			Assert.IsType<string []> (ctorArg);
			var arr = (string []) ctorArg;
			Assert.Equal (new [] { "auth1", "auth2" }, arr);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_EmptyFile ()
	{
		var peers = new List<JavaPeerInfo> ();

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			Assert.Empty (deserialized);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_NoComponentTypes ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				ManagedTypeName = "Test.Helper",
				JavaName = "test/Helper",
				AssemblyName = "TestAssembly",
				// No ComponentData
			},
		};

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			Assert.Empty (deserialized);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void Deserialize_NonExistentFile_ReturnsEmpty ()
	{
		var result = ComponentDataSerializer.Deserialize ("/nonexistent/path/file.txt");
		Assert.Empty (result);
	}

	[Fact]
	public void SerializeAndDeserialize_PreservesAbstractFlag ()
	{
		using var scanner = new JavaPeerScanner ();
		var peers = scanner.Scan (new [] { TestFixtureAssemblyPath });

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			var baseActivity = deserialized.FirstOrDefault (d => d.FullName == "MyApp.BaseActivity");
			Assert.NotNull (baseActivity);
			Assert.True (baseActivity!.IsAbstract);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_StringWithSpecialChars ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				ManagedTypeName = "Test.Type1",
				JavaName = "test/Type1",
				AssemblyName = "TestAssembly",
				ComponentData = new ComponentData {
					ComponentKind = ManifestComponentKind.Activity,
					ComponentAttribute = new ComponentAttributeInfo {
						AttributeType = "Android.App.ActivityAttribute",
						Properties = new Dictionary<string, object> {
							["Label"] = "Line1\\nLine2",
							["Description"] = "Has=equals",
						},
						ConstructorArguments = new List<object> (),
					},
				},
			},
		};

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			Assert.Single (deserialized);
			var type = deserialized [0];
			Assert.Equal ("Line1\\nLine2", type.ComponentAttribute!.Properties ["Label"]);
			Assert.Equal ("Has=equals", type.ComponentAttribute.Properties ["Description"]);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_JavaName_ConvertedToDotNotation ()
	{
		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				ManagedTypeName = "Test.Type1",
				JavaName = "com/example/Type1",
				AssemblyName = "TestAssembly",
				ComponentData = new ComponentData {
					ComponentKind = ManifestComponentKind.Activity,
					ComponentAttribute = new ComponentAttributeInfo {
						AttributeType = "Android.App.ActivityAttribute",
						Properties = new Dictionary<string, object> (),
						ConstructorArguments = new List<object> (),
					},
				},
			},
		};

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			Assert.Single (deserialized);
			Assert.Equal ("com.example.Type1", deserialized [0].JavaName);
		} finally {
			File.Delete (tempFile);
		}
	}

	[Fact]
	public void SerializeAndDeserialize_MultipleSubAttributes ()
	{
		var componentData = new ComponentData {
			ComponentKind = ManifestComponentKind.Activity,
			ComponentAttribute = new ComponentAttributeInfo {
				AttributeType = "Android.App.ActivityAttribute",
				Properties = new Dictionary<string, object> (),
				ConstructorArguments = new List<object> (),
			},
		};
		componentData.IntentFilters.Add (new ComponentAttributeInfo {
			AttributeType = "Android.App.IntentFilterAttribute",
			Properties = new Dictionary<string, object> { ["Priority"] = 10 },
			ConstructorArguments = new List<object> { new string [] { "action1" } },
		});
		componentData.IntentFilters.Add (new ComponentAttributeInfo {
			AttributeType = "Android.App.IntentFilterAttribute",
			Properties = new Dictionary<string, object> { ["Priority"] = 20 },
			ConstructorArguments = new List<object> { new string [] { "action2" } },
		});
		componentData.MetaDataEntries.Add (new ComponentAttributeInfo {
			AttributeType = "Android.App.MetaDataAttribute",
			Properties = new Dictionary<string, object> { ["Value"] = "val1" },
			ConstructorArguments = new List<object> { "key1" },
		});

		var peers = new List<JavaPeerInfo> {
			new JavaPeerInfo {
				ManagedTypeName = "Test.Type1",
				JavaName = "test/Type1",
				AssemblyName = "TestAssembly",
				ComponentData = componentData,
			},
		};

		var tempFile = Path.GetTempFileName ();
		try {
			ComponentDataSerializer.Serialize (peers, tempFile);
			var deserialized = ComponentDataSerializer.Deserialize (tempFile);

			Assert.Single (deserialized);
			var type = deserialized [0];
			Assert.Equal (2, type.IntentFilters.Count);
			Assert.Single (type.MetaDataEntries);
		} finally {
			File.Delete (tempFile);
		}
	}
}
