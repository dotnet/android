using System.Collections.Generic;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ManifestTypeInfoTests
{
	[Theory]
	[InlineData (0)] // None
	[InlineData (1)] // Activity
	[InlineData (2)] // Service
	[InlineData (3)] // BroadcastReceiver
	[InlineData (4)] // ContentProvider
	[InlineData (5)] // Application
	[InlineData (6)] // Instrumentation
	public void ComponentKind_AllValuesValid (int kindInt)
	{
		var kind = (ManifestComponentKind)kindInt;
		var info = new ManifestTypeInfo { ComponentKind = kind };
		Assert.Equal (kind, info.ComponentKind);
	}

	[Fact]
	public void DefaultValues_AreCorrect ()
	{
		var info = new ManifestTypeInfo ();

		Assert.Equal ("", info.FullName);
		Assert.Equal ("", info.Namespace);
		Assert.Equal ("", info.JavaName);
		Assert.Equal ("", info.CompatJavaName);
		Assert.False (info.IsAbstract);
		Assert.False (info.HasPublicParameterlessConstructor);
		Assert.Equal (ManifestComponentKind.None, info.ComponentKind);
		Assert.Null (info.ComponentAttribute);
		Assert.Empty (info.IntentFilters);
		Assert.Empty (info.MetaDataEntries);
		Assert.Empty (info.PropertyAttributes);
		Assert.Null (info.LayoutAttribute);
		Assert.Empty (info.GrantUriPermissions);
	}

	[Fact]
	public void AllProperties_CanBeSet ()
	{
		var componentAttr = new ComponentAttributeInfo {
			AttributeType = "Android.App.ActivityAttribute",
			Properties = new Dictionary<string, object> { { "MainLauncher", true } },
		};

		var intentFilter = new ComponentAttributeInfo {
			AttributeType = "Android.App.IntentFilterAttribute",
			ConstructorArguments = new object [] { new string [] { "android.intent.action.MAIN" } },
		};

		var metaData = new ComponentAttributeInfo {
			AttributeType = "Android.App.MetaDataAttribute",
			ConstructorArguments = new object [] { "key" },
			Properties = new Dictionary<string, object> { { "Value", "val" } },
		};

		var layout = new ComponentAttributeInfo {
			AttributeType = "Android.App.LayoutAttribute",
			Properties = new Dictionary<string, object> { { "DefaultWidth", "500dp" } },
		};

		var property = new ComponentAttributeInfo {
			AttributeType = "Android.App.PropertyAttribute",
			ConstructorArguments = new object [] { "prop.name" },
			Properties = new Dictionary<string, object> { { "Value", "prop.val" } },
		};

		var grantUri = new ComponentAttributeInfo {
			AttributeType = "Android.Content.GrantUriPermissionAttribute",
			Properties = new Dictionary<string, object> { { "Path", "/data" } },
		};

		var info = new ManifestTypeInfo {
			FullName = "MyApp.MainActivity",
			Namespace = "MyApp",
			JavaName = "my.app.MainActivity",
			CompatJavaName = "md5hash.MainActivity",
			IsAbstract = false,
			HasPublicParameterlessConstructor = true,
			ComponentKind = ManifestComponentKind.Activity,
			ComponentAttribute = componentAttr,
			IntentFilters = new [] { intentFilter },
			MetaDataEntries = new [] { metaData },
			PropertyAttributes = new [] { property },
			LayoutAttribute = layout,
			GrantUriPermissions = new [] { grantUri },
		};

		Assert.Equal ("MyApp.MainActivity", info.FullName);
		Assert.Equal ("MyApp", info.Namespace);
		Assert.Equal ("my.app.MainActivity", info.JavaName);
		Assert.Equal ("md5hash.MainActivity", info.CompatJavaName);
		Assert.False (info.IsAbstract);
		Assert.True (info.HasPublicParameterlessConstructor);
		Assert.Equal (ManifestComponentKind.Activity, info.ComponentKind);
		Assert.NotNull (info.ComponentAttribute);
		Assert.Single (info.IntentFilters);
		Assert.Single (info.MetaDataEntries);
		Assert.Single (info.PropertyAttributes);
		Assert.NotNull (info.LayoutAttribute);
		Assert.Single (info.GrantUriPermissions);
	}

	[Fact]
	public void ComponentAttributeInfo_DefaultValues ()
	{
		var attr = new ComponentAttributeInfo ();

		Assert.Equal ("", attr.AttributeType);
		Assert.NotNull (attr.Properties);
		Assert.Empty (attr.Properties);
		Assert.NotNull (attr.ConstructorArguments);
		Assert.Empty (attr.ConstructorArguments);
	}

	[Fact]
	public void ComponentAttributeInfo_CanStoreVariousPropertyTypes ()
	{
		var props = new Dictionary<string, object> {
			{ "StringProp", "hello" },
			{ "BoolProp", true },
			{ "IntProp", 42 },
			{ "ArrayProp", new string [] { "a", "b" } },
			{ "TypeProp", "MyApp.SomeType" },  // Type-valued props stored as type name strings
		};

		var attr = new ComponentAttributeInfo {
			AttributeType = "Test.Attribute",
			Properties = props,
		};

		Assert.Equal ("hello", attr.Properties ["StringProp"]);
		Assert.Equal (true, attr.Properties ["BoolProp"]);
		Assert.Equal (42, attr.Properties ["IntProp"]);
		Assert.IsType<string []> (attr.Properties ["ArrayProp"]);
		Assert.Equal ("MyApp.SomeType", attr.Properties ["TypeProp"]);
	}

	[Fact]
	public void IManifestTypeInfo_ImplementedByManifestTypeInfo ()
	{
		IManifestTypeInfo info = new ManifestTypeInfo {
			FullName = "Test.Type",
			ComponentKind = ManifestComponentKind.Service,
		};

		Assert.Equal ("Test.Type", info.FullName);
		Assert.Equal (ManifestComponentKind.Service, info.ComponentKind);
	}
}
