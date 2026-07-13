using System;
using System.Collections.Generic;
using System.Linq;

using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class PrecompiledTypeMapUniverseBuilderTests
{
	static TypeMapAssemblyData Model (string typeMapAssemblyName, params JavaPeerProxyData[] proxies)
	{
		var model = new TypeMapAssemblyData {
			AssemblyName = typeMapAssemblyName,
			ModuleName = typeMapAssemblyName + ".dll",
		};
		model.ProxyTypes.AddRange (proxies);
		return model;
	}

	static JavaPeerProxyData Proxy (string typeName, string jniName, string targetName, string targetAsm,
		string? invokerName = null, string? invokerAsm = null) =>
		new () {
			TypeName = typeName,
			JniName = jniName,
			TargetType = new TypeRefData { ManagedTypeName = targetName, AssemblyName = targetAsm },
			InvokerType = invokerName is null ? null : new TypeRefData { ManagedTypeName = invokerName, AssemblyName = invokerAsm! },
		};

	[Fact]
	public void SharedUniverse_GroupsExternalByJniName_AndKeysProxyByTarget ()
	{
		var model = Model ("_App.TypeMap",
			Proxy ("Android_App_Activity_Proxy", "android/app/Activity", "Android.App.Activity", "Mono.Android"));

		var universe = PrecompiledTypeMapUniverseBuilder.Build (new [] { model }, useSharedTypemapUniverse: true).Single ();

		var external = Assert.Single (universe.External);
		Assert.Equal ("android/app/Activity", external.Key);
		var proxyRef = Assert.Single (external.Value);
		Assert.Equal ("_App.TypeMap", proxyRef.AssemblyName);
		Assert.Equal ("_TypeMap.Proxies.Android_App_Activity_Proxy", proxyRef.FullTypeName);

		var proxy = Assert.Single (universe.Proxy);
		Assert.Equal ("Android.App.Activity, Mono.Android", proxy.Key);
		Assert.Equal (proxyRef, proxy.Value);
	}

	[Fact]
	public void AliasGroup_FlattensIntoMultiValueExternalEntry ()
	{
		var model = Model ("_App.TypeMap",
			Proxy ("JavaCollection_Proxy", "java/util/Collection", "MyApp.JavaCollection", "App"),
			Proxy ("JavaCollection_1_Proxy", "java/util/Collection", "MyApp.JavaCollection`1", "App"));

		var universe = PrecompiledTypeMapUniverseBuilder.Build (new [] { model }, useSharedTypemapUniverse: true).Single ();

		var external = Assert.Single (universe.External);
		Assert.Equal ("java/util/Collection", external.Key);
		Assert.Equal (2, external.Value.Count);
		Assert.Contains (external.Value, p => p.FullTypeName.EndsWith ("JavaCollection_Proxy"));
		Assert.Contains (external.Value, p => p.FullTypeName.EndsWith ("JavaCollection_1_Proxy"));

		// Both target types are distinct keys in the proxy map.
		Assert.Equal (2, universe.Proxy.Count);
	}

	[Fact]
	public void InvokerType_ProducesSecondProxyMapEntryForSameProxy ()
	{
		var model = Model ("_App.TypeMap",
			Proxy ("IMyListener_Proxy", "my/MyListener", "MyApp.IMyListener", "App",
				invokerName: "MyApp.IMyListenerInvoker", invokerAsm: "App"));

		var universe = PrecompiledTypeMapUniverseBuilder.Build (new [] { model }, useSharedTypemapUniverse: true).Single ();

		Assert.Equal (2, universe.Proxy.Count);
		var keys = universe.Proxy.Select (p => p.Key).ToList ();
		Assert.Contains ("MyApp.IMyListener, App", keys);
		Assert.Contains ("MyApp.IMyListenerInvoker, App", keys);
		// Both keys resolve to the same proxy.
		Assert.Single (universe.Proxy.Select (p => p.Value).Distinct ());
	}

	[Fact]
	public void SharedUniverse_MergesAllModelsIntoOne ()
	{
		var monoAndroid = Model ("_Mono.Android.TypeMap",
			Proxy ("Android_App_Activity_Proxy", "android/app/Activity", "Android.App.Activity", "Mono.Android"));
		var app = Model ("_App.TypeMap",
			Proxy ("MyActivity_Proxy", "myapp/MyActivity", "MyApp.MyActivity", "App"));

		var universes = PrecompiledTypeMapUniverseBuilder.Build (new [] { monoAndroid, app }, useSharedTypemapUniverse: true);

		var universe = Assert.Single (universes);
		Assert.Equal (2, universe.External.Count);
		Assert.Equal (2, universe.Proxy.Count);
	}

	[Fact]
	public void PerAssemblyUniverses_ProduceOneUniversePerModel ()
	{
		var monoAndroid = Model ("_Mono.Android.TypeMap",
			Proxy ("Android_App_Activity_Proxy", "android/app/Activity", "Android.App.Activity", "Mono.Android"));
		var app = Model ("_App.TypeMap",
			Proxy ("MyActivity_Proxy", "myapp/MyActivity", "MyApp.MyActivity", "App"));

		var universes = PrecompiledTypeMapUniverseBuilder.Build (new [] { monoAndroid, app }, useSharedTypemapUniverse: false);

		Assert.Equal (2, universes.Count);
		Assert.Equal ("android/app/Activity", universes [0].External.Single ().Key);
		Assert.Equal ("myapp/MyActivity", universes [1].External.Single ().Key);
	}
}
