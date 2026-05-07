using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ModelBuilderTests : FixtureTestBase
{
	static TypeMapAssemblyData BuildModel (IReadOnlyList<JavaPeerInfo> peers, string? assemblyName = null)
	{
		var outputPath = Path.Combine (Path.GetTempPath (), (assemblyName ?? "TestTypeMap") + ".dll");
		return ModelBuilder.Build (peers, outputPath, assemblyName);
	}

	static TypeMapAssemblyData BuildModelWithArrays (IReadOnlyList<JavaPeerInfo> peers, string? assemblyName = null, int maxArrayRank = 3)
	{
		var outputPath = Path.Combine (Path.GetTempPath (), (assemblyName ?? "TestTypeMap") + ".dll");
		return ModelBuilder.Build (peers, outputPath, assemblyName, maxArrayRank);
	}

	public class BasicStructure
	{
		[Fact]
		public void Build_EmptyPeers_ProducesEmptyModel ()
		{
			var model = BuildModel ([], "Empty");
			Assert.Equal ("Empty", model.AssemblyName);
			Assert.Equal ("Empty.dll", model.ModuleName);
			Assert.Empty (model.Entries);
			Assert.Empty (model.ProxyTypes);
		}

		[Theory]
		[InlineData ("Foo.Bar.dll", null, "Foo.Bar")]
		[InlineData ("Foo.dll", "MyAssembly", "MyAssembly")]
		public void Build_AssemblyName_ResolvedCorrectly (string outputPath, string? explicitName, string expected)
		{
			var model = ModelBuilder.Build ([], outputPath, explicitName);
			Assert.Equal (expected, model.AssemblyName);
		}
	}

	public class TypeMapEntries
	{
		[Fact]
		public void Build_CreatesOneEntryPerPeer ()
		{
			var peers = new List<JavaPeerInfo> {
				MakeMcwPeer ("java/lang/Object", "Java.Lang.Object", "Mono.Android"),
				MakeMcwPeer ("android/app/Activity", "Android.App.Activity", "Mono.Android"),
			};

			var model = BuildModel (peers);
			Assert.Equal (2, model.Entries.Count);
			Assert.Equal ("android/app/Activity", model.Entries [0].JniName);
			Assert.Equal ("java/lang/Object", model.Entries [1].JniName);
		}

		[Fact]
		public void Build_DuplicateJniNames_CreatesAliasEntries ()
		{
			var peers = new List<JavaPeerInfo> {
				MakeMcwPeer ("test/Dup", "Test.First", "A"),
				MakeMcwPeer ("test/Dup", "Test.Second", "A"),
			};

			var model = BuildModel (peers);
			// Three entries: "test/Dup[0]", "test/Dup[1]", and the base "test/Dup" → alias holder
			Assert.Equal (3, model.Entries.Count);
			Assert.Equal ("test/Dup[0]", model.Entries [0].JniName);
			Assert.Contains ("Test.First", model.Entries [0].ProxyTypeReference);
			Assert.Equal ("test/Dup[1]", model.Entries [1].JniName);
			Assert.Contains ("Test.Second", model.Entries [1].ProxyTypeReference);
			Assert.Equal ("test/Dup", model.Entries [2].JniName);

			// Both peers get associations to the alias holder
			Assert.Equal (2, model.Associations.Count);

			// One alias holder
			Assert.Single (model.AliasHolders);
			Assert.Equal (2, model.AliasHolders [0].AliasKeys.Count);
		}

		[Fact]
		public void Build_ThreeWayAlias_CreatesCorrectIndexedEntries ()
		{
			var peers = new List<JavaPeerInfo> {
				MakePeerWithActivation ("test/Triple", "Test.Alpha", "A"),
				MakePeerWithActivation ("test/Triple", "Test.Beta", "A"),
				MakePeerWithActivation ("test/Triple", "Test.Gamma", "A"),
			};

			var model = BuildModel (peers, "TripleAlias");
			// 3 indexed entries + 1 base entry → alias holder = 4
			Assert.Equal (4, model.Entries.Count);
			Assert.Equal ("test/Triple[0]", model.Entries [0].JniName);
			Assert.Equal ("test/Triple[1]", model.Entries [1].JniName);
			Assert.Equal ("test/Triple[2]", model.Entries [2].JniName);
			Assert.Equal ("test/Triple", model.Entries [3].JniName);

			// All three peers get associations to the alias holder
			Assert.Equal (3, model.Associations.Count);

			// Three distinct proxy types
			Assert.Equal (3, model.ProxyTypes.Count);

			// One alias holder with 3 keys
			Assert.Single (model.AliasHolders);
			Assert.Equal (3, model.AliasHolders [0].AliasKeys.Count);
		}

		[Fact]
		public void Build_AliasWithMixedActivation_PrimaryNoActivation_AliasHasActivation ()
		{
			var peers = new List<JavaPeerInfo> {
				MakeMcwPeer ("test/Mixed", "Test.NoAct", "A"),
				MakePeerWithActivation ("test/Mixed", "Test.WithAct", "A"),
			};

			var model = BuildModel (peers, "MixedAlias");
			// 2 indexed entries + 1 base entry → alias holder = 3
			Assert.Equal (3, model.Entries.Count);
			Assert.Equal ("test/Mixed[0]", model.Entries [0].JniName);
			Assert.Equal ("test/Mixed[1]", model.Entries [1].JniName);
			Assert.Equal ("test/Mixed", model.Entries [2].JniName);

			// Only the alias peer with activation gets a proxy
			Assert.Single (model.ProxyTypes);
			Assert.Equal ("Test_WithAct_Proxy", model.ProxyTypes [0].TypeName);

			// Both peers get associations to alias holder
			Assert.Equal (2, model.Associations.Count);
		}
	}

	public class ConditionalAttributes
	{
		[Theory]
		[InlineData ("java/lang/Object")]
		[InlineData ("java/lang/Throwable")]
		[InlineData ("java/lang/Exception")]
		[InlineData ("java/lang/RuntimeException")]
		[InlineData ("java/lang/Error")]
		[InlineData ("java/lang/Class")]
		[InlineData ("java/lang/String")]
		[InlineData ("java/lang/Thread")]
		public void Build_AllEssentialRuntimeTypes_AreUnconditional (string jniName)
		{
			var peer = MakeMcwPeer (jniName, "Java.Lang.SomeType", "Mono.Android") with { DoNotGenerateAcw = true };
			var model = BuildModel (new [] { peer });
			Assert.True (model.Entries [0].IsUnconditional, $"{jniName} should be unconditional");
		}

		[Fact]
		public void Build_UserAcwType_IsUnconditional ()
		{
			// User-defined ACW types (not MCW, not interface) are unconditional
			// because Android can instantiate them from Java
			var peer = MakeAcwPeer ("my/app/Main", "MyApp.MainActivity", "App");
			var model = BuildModel (new [] { peer });

			var mainEntry = model.Entries.First (e => e.JniName == "my/app/Main");
			Assert.True (mainEntry.IsUnconditional);
			Assert.Null (mainEntry.TargetTypeReference);
		}

		[Fact]
		public void Build_McwBinding_IsTrimmable ()
		{
			// MCW binding types (DoNotGenerateAcw=true) are trimmable unless essential.
			// When ForceUnconditionalEntries is enabled (workaround for dotnet/runtime#127004),
			// all entries become unconditional.
			var peer = MakeMcwPeer ("android/app/Activity", "Android.App.Activity", "Mono.Android") with { DoNotGenerateAcw = true };
			var model = BuildModel (new [] { peer });

			Assert.Single (model.Entries);
			Assert.True (model.Entries [0].IsUnconditional);
			Assert.Null (model.Entries [0].TargetTypeReference);
		}

		[Fact]
		public void Build_UnconditionalScannedType_IsUnconditional ()
		{
			// Types with IsUnconditional from scanner (e.g., from [Activity], [Service] attrs)
			var peer = MakeMcwPeer ("my/app/MySvc", "MyApp.MyService", "App") with {
				DoNotGenerateAcw = true, // simulate MCW-like
				IsUnconditional = true, // scanner marked it
			};
			var model = BuildModel (new [] { peer });

			Assert.True (model.Entries [0].IsUnconditional);
		}
	}

	public class Aliases
	{
		[Fact]
		public void Build_AliasedPeersWithActivation_GetDistinctProxies ()
		{
			var peers = new List<JavaPeerInfo> {
				MakePeerWithActivation ("test/Dup", "Test.First", "A"),
				MakePeerWithActivation ("test/Dup", "Test.Second", "A"),
			};

			var model = BuildModel (peers, "TypeMap");
			Assert.Equal (2, model.ProxyTypes.Count);
			Assert.Equal ("Test_First_Proxy", model.ProxyTypes [0].TypeName);
			Assert.Equal ("Test_Second_Proxy", model.ProxyTypes [1].TypeName);
		}

		[Fact]
		public void Build_McwPeerWithoutActivation_NoProxy ()
		{
			var peer = MakeMcwPeer ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
			var model = BuildModel (new [] { peer });

			Assert.Empty (model.ProxyTypes);
			Assert.Single (model.Entries);
			Assert.Contains ("Java.Lang.Object, Mono.Android", model.Entries [0].ProxyTypeReference);
		}
	}

	public class ProxyTypes
	{
		[Theory]
		[InlineData ("java/lang/Object", "Java.Lang.Object", "Mono.Android", "Java_Lang_Object_Proxy")]
		[InlineData ("com/example/Outer$Inner", "Com.Example.Outer.Inner", "App", "Com_Example_Outer_Inner_Proxy")]
		[InlineData ("my/app/GenericHolder", "MyApp.Generic.GenericHolder`1", "App", "MyApp_Generic_GenericHolder_1_Proxy")]
		public void Build_PeerWithActivation_CreatesNamedProxy (string jniName, string managedName, string asmName, string expectedProxyName)
		{
			var peer = MakePeerWithActivation (jniName, managedName, asmName);
			var model = BuildModel (new [] { peer }, "MyTypeMap");

			Assert.Single (model.ProxyTypes);
			var proxy = model.ProxyTypes [0];
			Assert.Equal (expectedProxyName, proxy.TypeName);
			Assert.Equal (jniName, proxy.JniName);
			Assert.Equal ("_TypeMap.Proxies", proxy.Namespace);
			Assert.True (proxy.HasActivation);
			Assert.Equal (managedName, proxy.TargetType.ManagedTypeName);
			Assert.Equal (asmName, proxy.TargetType.AssemblyName);
		}

		[Fact]
		public void Build_SinglePeer_HasAssociation ()
		{
			// When ForceUnconditionalEntries is enabled, single peers emit associations
			// so the runtime proxy type map is populated.
			var peer = MakePeerWithActivation ("my/app/MainActivity", "MyApp.MainActivity", "App");
			var model = BuildModel (new [] { peer }, "MyTypeMap");

			Assert.Single (model.Associations);
		}

		[Fact]
		public void Build_PeerWithInvoker_CreatesProxy ()
		{
			var peer = MakeInterfacePeer ("android/view/View$OnClickListener", "Android.Views.View+IOnClickListener", "Mono.Android", "Android.Views.View+IOnClickListenerInvoker");

			var model = BuildModel (new [] { peer });
			Assert.Single (model.ProxyTypes);
			var proxy = model.ProxyTypes [0];
			Assert.NotNull (proxy.InvokerType);
			Assert.Equal ("Android.Views.View+IOnClickListenerInvoker", proxy.InvokerType!.ManagedTypeName);
		}

		[Theory]
		[InlineData ("MyApp.PlainActivitySubclass")]
		[InlineData ("MyApp.UnnamedActivity")]
		[InlineData ("MyApp.UnregisteredClickListener")]
		[InlineData ("MyApp.UnregisteredExporter")]
		[InlineData ("MyApp.UnregisteredHelper")]
		[InlineData ("MyApp.DerivedFromComponentBase")]
		public void Build_Crc64RenamedPeer_StoresFinalJavaNameOnProxy (string managedName)
		{
			var peer = FindFixtureByManagedName (managedName);
			Assert.StartsWith ("crc64", peer.JavaName);
			Assert.NotEqual (peer.CompatJniName, peer.JavaName);

			var model = BuildModel (new [] { peer }, "MyTypeMap");
			var proxy = Assert.Single (model.ProxyTypes);

			Assert.Equal (peer.JavaName, proxy.JniName);
		}
	}

	public class FixtureScan
	{
		[Fact]
		public void Build_FromScannedFixtures_ProducesValidModel ()
		{
			var peers = ScanFixtures ();
			var model = BuildModel (peers, "TestTypeMap");

			Assert.Equal ("TestTypeMap", model.AssemblyName);
			Assert.NotEmpty (model.Entries);
			Assert.NotEmpty (model.ProxyTypes);

			Assert.All (model.Entries, e => Assert.False (string.IsNullOrEmpty (e.JniName)));
			Assert.All (model.Entries, e => Assert.False (string.IsNullOrEmpty (e.ProxyTypeReference)));
		}

		[Theory]
		[InlineData ("my/app/MainActivity", "MainActivity")]
		[InlineData ("android/app/Activity", "Activity")]
		[InlineData ("java/lang/Object", "Object")]
		[InlineData ("my/app/Outer$Inner", "Inner")]
		[InlineData ("my/app/ICallback$Result", "Result")]
		public void ScanFixtures_ManagedTypeShortName_IsCorrect (string javaName, string expectedShortName)
		{
			var peer = FindFixtureByJavaName (javaName);
			Assert.Equal (expectedShortName, peer.ManagedTypeShortName);
		}
	}

	public class FixtureConditionalAttributes
	{
		[Theory]
		[InlineData ("my/app/MainActivity")]
		[InlineData ("my/app/TouchHandler")]
		public void Fixture_UserAcwType_IsUnconditional (string javaName)
		{
			var peer = FindFixtureByJavaName (javaName);
			Assert.False (peer.DoNotGenerateAcw);
			var model = BuildModel (new [] { peer });
			Assert.True (model.Entries [0].IsUnconditional);
		}

		[Theory]
		[InlineData ("android/app/Activity")]
		[InlineData ("android/widget/Button")]
		public void Fixture_McwBinding_IsTrimmable (string javaName)
		{
			var peer = FindFixtureByJavaName (javaName);
			Assert.True (peer.DoNotGenerateAcw);
			var model = BuildModel (new [] { peer });
			// ForceUnconditionalEntries workaround makes all entries unconditional
			Assert.True (model.Entries [0].IsUnconditional);
		}
	}

	static JavaPeerProxyData? FindProxy (TypeMapAssemblyData model, string proxyTypeName)
	{
		return model.ProxyTypes.FirstOrDefault (p => p.TypeName == proxyTypeName);
	}

	static TypeMapAttributeData? FindEntry (TypeMapAssemblyData model, string jniName)
	{
		return model.Entries.FirstOrDefault (e => e.JniName == jniName);
	}

	public class FixtureMcwTypes
	{
		[Theory]
		[InlineData ("java/lang/Object", "Java_Lang_Object_Proxy", "Java.Lang.Object")]
		[InlineData ("android/app/Activity", "Android_App_Activity_Proxy", "Android.App.Activity")]
		[InlineData ("java/lang/Throwable", "Java_Lang_Throwable_Proxy", "Java.Lang.Throwable")]
		[InlineData ("java/lang/Exception", "Java_Lang_Exception_Proxy", "Java.Lang.Exception")]
		public void Fixture_McwType_HasActivation_CreatesProxy (string javaName, string expectedProxyName, string expectedManagedName)
		{
			var peer = FindFixtureByJavaName (javaName);
			var model = BuildModel (new [] { peer }, "TypeMap");

			var proxy = FindProxy (model, expectedProxyName);
			Assert.NotNull (proxy);
			Assert.True (proxy!.HasActivation);
			Assert.Equal (expectedManagedName, proxy.TargetType.ManagedTypeName);
		}

		[Fact]
		public void Fixture_Activity_Entry_PointsToProxy ()
		{
			var peer = FindFixtureByJavaName ("android/app/Activity");
			var model = BuildModel (new [] { peer }, "MyTypeMap");

			var entry = FindEntry (model, "android/app/Activity");
			Assert.NotNull (entry);
			Assert.Contains ("Android_App_Activity_Proxy", entry!.ProxyTypeReference);
			Assert.Contains ("MyTypeMap", entry.ProxyTypeReference);
		}

		[Fact]
		public void Fixture_Service_NoActivation_NoProxy ()
		{
			// Service in fixtures has no activation ctor on its own — it inherits from J.L.Object
			// but Service itself has `protected Service(IntPtr, JniHandleOwnership)` which IS an activation ctor
			var peer = FindFixtureByJavaName ("android/app/Service");
			var model = BuildModel (new [] { peer }, "TypeMap");

			if (peer.ActivationCtor != null) {
				Assert.Single (model.ProxyTypes);
			} else {
				Assert.Empty (model.ProxyTypes);
			}
		}
	}

	public class FixtureCustomView
	{
		[Fact]
		public void Fixture_CustomView_HasTwoConstructors ()
		{
			var peer = FindFixtureByJavaName ("my/app/CustomView");

			var model = BuildModel (new [] { peer }, "TypeMap");
			var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "MyApp_CustomView_Proxy");
			Assert.NotNull (proxy);
		}
	}

	public class FixtureInterfaces
	{
		[Fact]
		public void Fixture_IOnClickListener_HasInvokerProxy ()
		{
			var peers = ScanFixtures ();
			var listener = peers.FirstOrDefault (p => p.ManagedTypeName == "Android.Views.IOnClickListener");
			Assert.NotNull (listener);
			Assert.True (listener!.IsInterface);
			Assert.NotNull (listener.InvokerTypeName);

			var model = BuildModel (new [] { listener }, "TypeMap");
			var proxy = model.ProxyTypes.FirstOrDefault ();
			Assert.NotNull (proxy);
			Assert.NotNull (proxy!.InvokerType);
			Assert.Equal ("Android.Views.IOnClickListenerInvoker", proxy.InvokerType!.ManagedTypeName);
		}
	}

	public class FixtureNestedTypes
	{
		[Theory]
		[InlineData ("my/app/Outer$Inner", "MyApp_Outer_Inner_Proxy", "MyApp.Outer+Inner")]
		[InlineData ("my/app/ICallback$Result", "MyApp_ICallback_Result_Proxy", "MyApp.ICallback+Result")]
		public void Fixture_NestedType_ProxyNaming (string javaName, string expectedProxyName, string expectedManagedName)
		{
			var peer = FindFixtureByJavaName (javaName);
			var model = BuildModel (new [] { peer }, "TypeMap");

			var entry = FindEntry (model, javaName);
			Assert.NotNull (entry);

			if (peer.ActivationCtor != null) {
				var proxy = FindProxy (model, expectedProxyName);
				Assert.NotNull (proxy);
				Assert.Equal (expectedManagedName, proxy!.TargetType.ManagedTypeName);
			}
		}
	}

	public class FixtureInvokers
	{
		[Fact]
		public void Fixture_InterfaceAndInvoker_ShareJniName_InvokerSeparated ()
		{
			var peers = ScanFixtures ();
			// IOnClickListener and IOnClickListenerInvoker share "android/view/View$OnClickListener"
			var clickPeers = peers.Where (p => p.JavaName == "android/view/View$OnClickListener").ToList ();
			Assert.Equal (2, clickPeers.Count);

			var model = BuildModel (clickPeers, "TypeMap");

			// Invoker is excluded from TypeMap entries/proxies. It still gets a
			// managed→proxy association so its JniPeerMembers can resolve the JNI name.
			Assert.Single (model.Entries);
			Assert.Equal ("android/view/View$OnClickListener", model.Entries [0].JniName);

			// Only the interface proxy exists; the invoker type is also referenced
			// as a TypeRef in the interface proxy's InvokerType property.
			Assert.Single (model.ProxyTypes);
			Assert.NotNull (model.ProxyTypes [0].InvokerType);
			Assert.Equal ("Android.Views.IOnClickListenerInvoker", model.ProxyTypes [0].InvokerType!.ManagedTypeName);
			Assert.Contains (model.Associations, a => a.SourceTypeReference == "Android.Views.IOnClickListenerInvoker, TestFixtures");
		}

		[Fact]
		public void Build_InvokerType_NoProxyNoEntry ()
		{
			// Invoker types should never get their own proxy or TypeMap entry.
			// They only appear as a TypeRef in the interface proxy's InvokerType/CreateInstance.
			var ifacePeer = MakeInterfacePeer ("my/app/IFoo", "MyApp.IFoo", "App", "MyApp.FooInvoker");
			var invokerPeer = MakePeerWithActivation ("my/app/IFoo", "MyApp.FooInvoker", "App") with { DoNotGenerateAcw = true };

			var model = BuildModel (new [] { ifacePeer, invokerPeer });

			// Only the interface gets a TypeMap entry — its ProxyTypeReference points to the generated proxy
			Assert.Single (model.Entries);
			Assert.Contains ("MyApp_IFoo_Proxy", model.Entries [0].ProxyTypeReference);

			// Only the interface gets a proxy — the invoker is referenced, not proxied
			Assert.Single (model.ProxyTypes);
			var proxy = model.ProxyTypes [0];
			Assert.Equal ("MyApp.IFoo", proxy.TargetType.ManagedTypeName);
			Assert.NotNull (proxy.InvokerType);
			Assert.Equal ("MyApp.FooInvoker", proxy.InvokerType!.ManagedTypeName);

			// Interface proxy has activation because it will create the invoker
			Assert.True (proxy.HasActivation);

			Assert.Equal (2, model.Associations.Count);
			Assert.Contains (model.Associations, a => a.SourceTypeReference == "MyApp.IFoo, App");
			Assert.Contains (model.Associations, a => a.SourceTypeReference == "MyApp.FooInvoker, App");
		}
	}

	public class FixtureAliases
	{
		[Fact]
		public void Fixture_AliasTarget_ThreeTypesShareJniName ()
		{
			var peers = ScanFixtures ();
			var aliasPeers = peers.Where (p => p.JavaName == "test/AliasTarget").ToList ();
			Assert.Equal (3, aliasPeers.Count);
		}

		[Fact]
		public void Fixture_AliasTarget_ProducesIndexedEntries ()
		{
			var peers = ScanFixtures ();
			var aliasPeers = peers.Where (p => p.JavaName == "test/AliasTarget").ToList ();

			var model = BuildModel (aliasPeers, "AliasFixture");

			// 3 indexed entries + 1 base entry → alias holder = 4
			Assert.Equal (4, model.Entries.Count);
			Assert.Equal ("test/AliasTarget[0]", model.Entries [0].JniName);
			Assert.Equal ("test/AliasTarget[1]", model.Entries [1].JniName);
			Assert.Equal ("test/AliasTarget[2]", model.Entries [2].JniName);
			Assert.Equal ("test/AliasTarget", model.Entries [3].JniName);
		}

		[Fact]
		public void Fixture_AliasTarget_EachPeerGetsDistinctProxy ()
		{
			var peers = ScanFixtures ();
			var aliasPeers = peers.Where (p => p.JavaName == "test/AliasTarget").ToList ();

			var model = BuildModel (aliasPeers, "AliasFixture");
			Assert.Equal (3, model.ProxyTypes.Count);

			var proxyNames = model.ProxyTypes.Select (p => p.TypeName).ToList ();
			Assert.Equal (proxyNames.Distinct ().Count (), proxyNames.Count);
		}

		[Fact]
		public void Fixture_AliasTarget_AssociationsLinkToAliasHolder ()
		{
			var peers = ScanFixtures ();
			var aliasPeers = peers.Where (p => p.JavaName == "test/AliasTarget").ToList ();

			var model = BuildModel (aliasPeers, "AliasFixture");
			// All 3 peers get associations to the alias holder
			Assert.Equal (3, model.Associations.Count);

			// All associations point to the same alias holder
			var holderRef = model.Associations [0].AliasProxyTypeReference;
			Assert.All (model.Associations, a => Assert.Equal (holderRef, a.AliasProxyTypeReference));
			Assert.Contains ("_Aliases", holderRef);
		}

		[Fact]
		public void Fixture_AliasTarget_GeneratesAliasHolder ()
		{
			var peers = ScanFixtures ();
			var aliasPeers = peers.Where (p => p.JavaName == "test/AliasTarget").ToList ();

			var model = BuildModel (aliasPeers, "AliasFixture");
			Assert.Single (model.AliasHolders);

			var holder = model.AliasHolders [0];
			Assert.Equal ("_TypeMap.Aliases", holder.Namespace);
			Assert.Equal (3, holder.AliasKeys.Count);
			Assert.Equal ("test/AliasTarget[0]", holder.AliasKeys [0]);
			Assert.Equal ("test/AliasTarget[1]", holder.AliasKeys [1]);
			Assert.Equal ("test/AliasTarget[2]", holder.AliasKeys [2]);
		}
	}

	public class FixtureGenericHolder
	{
		[Fact]
		public void Fixture_GenericHolder_Entry ()
		{
			var peer = FindFixtureByJavaName ("my/app/GenericHolder");
			Assert.True (peer.IsGenericDefinition);

			var model = BuildModel (new [] { peer }, "TypeMap");
			var entry = FindEntry (model, "my/app/GenericHolder");
			Assert.NotNull (entry);
		}

		[Fact]
		public void Fixture_GenericHolder_HasAssociation ()
		{
			// Generic definitions must still get a TypeMapAssociation entry so managed→proxy
			// lookup works for the open generic definition. Their proxy derives from the
			// non-generic `JavaPeerProxy` base, so the CLR can load the proxy without
			// resolving an open generic argument.
			var peer = FindFixtureByJavaName ("my/app/GenericHolder");
			Assert.True (peer.IsGenericDefinition);

			var model = BuildModel (new [] { peer }, "TypeMap");
			Assert.Contains (model.Associations,
				a => a.SourceTypeReference.StartsWith ("MyApp.Generic.GenericHolder`1", StringComparison.Ordinal));
		}
	}

	public class FixtureAcwTypeHasProxy
	{
		[Theory]
		[InlineData ("my/app/AbstractBase", "MyApp_AbstractBase_Proxy")]
		[InlineData ("my/app/ClickableView", "MyApp_ClickableView_Proxy")]
		[InlineData ("my/app/MultiInterfaceView", "MyApp_MultiInterfaceView_Proxy")]
		[InlineData ("my/app/ExportExample", "MyApp_ExportExample_Proxy")]
		public void Fixture_AcwType_HasProxy (string javaName, string expectedProxyName)
		{
			var peer = FindFixtureByJavaName (javaName);
			Assert.False (peer.DoNotGenerateAcw);

			var model = BuildModel (new [] { peer }, "TypeMap");

			if (peer.ActivationCtor != null) {
				var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == expectedProxyName);
				Assert.NotNull (proxy);
			}
		}
	}

	public class FixtureImplementorsAndDispatchers
	{
		[Theory]
		[InlineData ("mono/android/view/View_IOnClickListenerImplementor", "Implementor")]
		[InlineData ("mono/android/view/View_ClickEventDispatcher", "EventDispatcher")]
		public void Fixture_HelperType_IsUnconditional (string javaName, string kind)
		{
			var peer = FindFixtureByJavaName (javaName);
			Assert.False (peer.DoNotGenerateAcw);
			Assert.False (peer.IsInterface);

			var model = BuildModel (new [] { peer }, "TypeMap");

			var entry = model.Entries.FirstOrDefault ();
			Assert.NotNull (entry);
			// Implementor/EventDispatcher types are treated as unconditional ACW types.
			// Future optimization (see #10911) may make them trimmable.
			Assert.True (entry.IsUnconditional, $"{kind} should be unconditional");
		}
	}

	public class InvokerDetection
	{
		[Fact]
		public void Build_TypeIsInvoker_OnlyWhenReferencedByAnotherPeer ()
		{
			// A type is only treated as an invoker when another peer's InvokerTypeName references it.
			// A type named "MyInvoker" with DoNotGenerateAcw is NOT automatically an invoker.
			var invokerPeer = MakePeerWithActivation ("my/app/MyInvoker", "MyApp.MyInvoker", "App") with { DoNotGenerateAcw = true };

			// Without a referencing peer, it gets a normal entry
			var model1 = BuildModel (new [] { invokerPeer });
			Assert.Single (model1.Entries);

			// When an interface references it as invoker, it is excluded
			var ifacePeer = MakeInterfacePeer ("my/app/MyInvoker", "MyApp.IMyInterface", "App", "MyApp.MyInvoker");
			var model2 = BuildModel (new [] { ifacePeer, invokerPeer });
			// Only the interface gets entries/proxies, the invoker is excluded
			Assert.Single (model2.Entries);
			Assert.Equal ("MyApp.IMyInterface", model2.ProxyTypes [0].TargetType.ManagedTypeName);
			Assert.Contains (model2.Associations, a => a.SourceTypeReference == "MyApp.MyInvoker, App");
		}
	}

	public class PipelineTests
	{
		[Fact]
		public void FullPipeline_AllFixtures_ProducesLoadableAssembly ()
		{
			var peers = ScanFixtures ();
			var model = BuildModel (peers, "FullPipeline");

			EmitAndVerify (model, "FullPipeline", (pe, reader) => {
				Assert.True (pe.HasMetadata);

				var asmDef = reader.GetAssemblyDefinition ();
				Assert.Equal ("FullPipeline", reader.GetString (asmDef.Name));

				var proxyTypes = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
					.ToList ();
				Assert.Equal (model.ProxyTypes.Count, proxyTypes.Count);

				var proxyNames = proxyTypes.Select (t => reader.GetString (t.Name)).OrderBy (n => n).ToList ();
				var modelNames = model.ProxyTypes.Select (p => p.TypeName).OrderBy (n => n).ToList ();
				Assert.Equal (modelNames, proxyNames);
			});
		}

		[Fact]
		public void FullPipeline_AllFixtures_TypeMapAttributeCountMatchesEntries ()
		{
			var peers = ScanFixtures ();
			var model = BuildModel (peers, "AttrCount");

			EmitAndVerify (model, "AttrCount", (pe, reader) => {
				var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				int totalAttrs = asmAttrs.Count ();

				int expected = model.Entries.Count + model.Associations.Count + model.IgnoresAccessChecksTo.Count;
				Assert.Equal (expected, totalAttrs);
			});
		}

		[Fact]
		public void FullPipeline_AliasGroup_TypeMapAttributeCountIncludesAssociations ()
		{
			// Two peers with the same JNI name, both with activation → generates an association
			var peers = new List<JavaPeerInfo> {
				MakePeerWithActivation ("test/Alias", "Test.Primary", "Asm"),
				MakePeerWithActivation ("test/Alias", "Test.Secondary", "Asm"),
			};
			var model = BuildModel (peers, "AliasAttrCount");
			Assert.NotEmpty (model.Associations);

			EmitAndVerify (model, "AliasAttrCount", (pe, reader) => {
				var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				int totalAttrs = asmAttrs.Count ();
				int expected = model.Entries.Count + model.Associations.Count + model.IgnoresAccessChecksTo.Count;
				Assert.Equal (expected, totalAttrs);
			});
		}

		[Fact]
		public void FullPipeline_CustomView_HasConstructorAndMethodWrappers ()
		{
			var peer = FindFixtureByJavaName ("my/app/CustomView");
			var model = BuildModel (new [] { peer }, "CtorTest");

			EmitAndVerify (model, "CtorTest", (pe, reader) => {
				var proxy = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.First (t => reader.GetString (t.Name) == "MyApp_CustomView_Proxy");

				var methodNames = proxy.GetMethods ()
					.Select (h => reader.GetString (reader.GetMethodDefinition (h).Name))
					.ToList ();

				Assert.Contains (".ctor", methodNames);
				Assert.Contains ("CreateInstance", methodNames);
			});
		}

		[Fact]
		public void FullPipeline_GenericHolder_ProducesValidAssembly ()
		{
			var peer = FindFixtureByJavaName ("my/app/GenericHolder");
			var model = BuildModel (new [] { peer }, "GenericTest");

			EmitAndVerify (model, "GenericTest", (pe, reader) => {
				Assert.True (pe.HasMetadata);
				var entry = FindEntry (model, "my/app/GenericHolder");
				Assert.NotNull (entry);

				var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				Assert.NotEmpty (asmAttrs);
			});
		}
	}

	public class PeBlobValidation
	{
		[Fact]
		public void FullPipeline_Mixed2ArgAnd3Arg_BothSurviveRoundTrip ()
		{
			// With ForceUnconditionalEntries, both are emitted as 2-arg unconditional
			var objectPeer = FindFixtureByJavaName ("java/lang/Object");
			var activityPeer = FindFixtureByJavaName ("android/app/Activity");

			var model = BuildModel (new [] { objectPeer, activityPeer }, "MixedBlob");
			Assert.Equal (2, model.Entries.Count);

			EmitAndVerify (model, "MixedBlob", (pe, reader) => {
				var attrs = ReadAllTypeMapAttributeBlobs (reader);
				Assert.Equal (2, attrs.Count);

				var objectEntry = attrs.FirstOrDefault (a => a.jniName == "java/lang/Object");
				Assert.NotNull (objectEntry.jniName);
				Assert.Null (objectEntry.targetRef);

				var activityEntry = attrs.FirstOrDefault (a => a.jniName == "android/app/Activity");
				Assert.NotNull (activityEntry.jniName);
				Assert.Null (activityEntry.targetRef); // unconditional due to ForceUnconditionalEntries
			});
		}

		[Theory]
		[InlineData ("java/lang/Object", "Blob2Arg", "Java_Lang_Object_Proxy")]
		[InlineData ("my/app/MainActivity", "BlobAcw", "MyApp_MainActivity_Proxy")]
		public void FullPipeline_UnconditionalType_Emits2ArgAttribute (string javaName, string assemblyName, string expectedProxyName)
		{
			var peer = FindFixtureByJavaName (javaName);
			var model = BuildModel (new [] { peer }, assemblyName);
			Assert.Single (model.Entries);
			Assert.True (model.Entries [0].IsUnconditional);

			EmitAndVerify (model, assemblyName, (pe, reader) => {
				var (jniName2, proxyRef, targetRef) = ReadFirstTypeMapAttributeBlob (reader);

				Assert.Equal (javaName, jniName2);
				Assert.NotNull (proxyRef);
				Assert.Contains (expectedProxyName, proxyRef!);
				Assert.Null (targetRef);
			});
		}

		[Fact]
		public void FullPipeline_McwBinding_Emits2ArgAttribute_WithWorkaround ()
		{
			// With ForceUnconditionalEntries workaround for dotnet/runtime#127004,
			// MCW bindings are emitted as 2-arg unconditional.
			var peer = FindFixtureByJavaName ("android/app/Activity");
			var model = BuildModel (new [] { peer }, "Blob2ArgWorkaround");
			Assert.Single (model.Entries);
			Assert.True (model.Entries [0].IsUnconditional);

			EmitAndVerify (model, "Blob2ArgWorkaround", (pe, reader) => {
				var (jniName, proxyRef, targetRef) = ReadFirstTypeMapAttributeBlob (reader);

				Assert.Equal ("android/app/Activity", jniName);
				Assert.NotNull (proxyRef);
				Assert.Contains ("Android_App_Activity_Proxy", proxyRef!);
				Assert.Null (targetRef); // unconditional due to ForceUnconditionalEntries
			});
		}
	}

	public class DeterminismTests
	{
		[Fact]
		public void Build_SameInput_ProducesDeterministicOutput ()
		{
			var peers = ScanFixtures ();

			var model1 = BuildModel (peers, "DetTest");
			var model2 = BuildModel (peers, "DetTest");

			Assert.Equal (model1.Entries.Count, model2.Entries.Count);
			for (int i = 0; i < model1.Entries.Count; i++) {
				Assert.Equal (model1.Entries [i].JniName, model2.Entries [i].JniName);
				Assert.Equal (model1.Entries [i].ProxyTypeReference, model2.Entries [i].ProxyTypeReference);
				Assert.Equal (model1.Entries [i].TargetTypeReference, model2.Entries [i].TargetTypeReference);
			}
		}
	}

	public class ArrayEntries
	{
		[Fact]
		public void Build_DefaultEmitArrayEntriesFalse_NoArrayEntries ()
		{
			var peer = MakeMcwPeer ("foo/Bar", "Foo.Bar", "App");
			var model = BuildModel (new [] { peer });

			Assert.Equal (0, model.MaxArrayRank);
			Assert.DoesNotContain (model.Entries, e => e.AnchorRank is not null);
		}

		[Fact]
		public void Build_EmitArrayEntries_SetsMaxArrayRank ()
		{
			var peer = MakeMcwPeer ("foo/Bar", "Foo.Bar", "App");
			var model = BuildModelWithArrays (new [] { peer });

			Assert.Equal (3, model.MaxArrayRank);
		}

		[Fact]
		public void Build_EmitArrayEntries_HonoursMaxArrayRank ()
		{
			// Caller can ask for fewer or more ranks than the default. Verifies the
			// $(_AndroidTrimmableTypeMapMaxArrayRank) MSBuild property's effect.
			var peer = MakeMcwPeer ("foo/Bar", "Foo.Bar", "App");

			var model5 = BuildModelWithArrays (new [] { peer }, maxArrayRank: 5);
			Assert.Equal (5, model5.MaxArrayRank);
			var rank5Entries = model5.Entries.Where (e => e.AnchorRank is not null).ToList ();
			Assert.Equal (5, rank5Entries.Count);
			Assert.Equal ("Foo.Bar[][][][][], App", rank5Entries.Single (e => e.AnchorRank == 5).TargetTypeReference);

			var model1 = BuildModelWithArrays (new [] { peer }, maxArrayRank: 1);
			Assert.Equal (1, model1.MaxArrayRank);
			Assert.Single (model1.Entries, e => e.AnchorRank is not null);
		}

		[Fact]
		public void Build_EmitArrayEntries_EmitsRanks1Through3 ()
		{
			var peer = MakeMcwPeer ("foo/Bar", "Foo.Bar", "App");
			var model = BuildModelWithArrays (new [] { peer });

			var arrayEntries = model.Entries.Where (e => e.AnchorRank is not null).ToList ();
			Assert.Equal (3, arrayEntries.Count);
			Assert.Equal (new int? [] { 1, 2, 3 }, arrayEntries.Select (e => e.AnchorRank).ToArray ());
			Assert.All (arrayEntries, e => Assert.Equal ("foo/Bar", e.JniName));
		}

		[Fact]
		public void Build_EmitArrayEntries_KeyIsElementJniName ()
		{
			// No "[L...;" prefix at runtime — the key is the bare element JNI name and rank
			// is encoded by which sentinel anchor (TGroup) the entry uses.
			var peer = MakeMcwPeer ("java/lang/String", "System.String", "System.Runtime");
			var model = BuildModelWithArrays (new [] { peer });

			var arrayEntries = model.Entries.Where (e => e.AnchorRank is not null).ToList ();
			Assert.All (arrayEntries, e => Assert.Equal ("java/lang/String", e.JniName));
			Assert.All (arrayEntries, e => Assert.False (e.JniName.StartsWith ("[", StringComparison.Ordinal)));
		}

		[Fact]
		public void Build_EmitArrayEntries_TrimTargetIsClosedArrayType ()
		{
			// 3rd ctor arg = the closed array type itself, so ILC's per-shape conditional
			// drops the entry when the array shape is never constructed.
			var peer = MakeMcwPeer ("foo/Bar", "Foo.Bar", "App");
			var model = BuildModelWithArrays (new [] { peer });

			var rank1 = model.Entries.Single (e => e.AnchorRank == 1);
			Assert.Equal ("Foo.Bar[], App",     rank1.ProxyTypeReference);
			Assert.Equal ("Foo.Bar[], App",     rank1.TargetTypeReference);
			var rank2 = model.Entries.Single (e => e.AnchorRank == 2);
			Assert.Equal ("Foo.Bar[][], App",   rank2.ProxyTypeReference);
			Assert.Equal ("Foo.Bar[][], App",   rank2.TargetTypeReference);
			var rank3 = model.Entries.Single (e => e.AnchorRank == 3);
			Assert.Equal ("Foo.Bar[][][], App", rank3.ProxyTypeReference);
			Assert.Equal ("Foo.Bar[][][], App", rank3.TargetTypeReference);
		}

		[Fact]
		public void Build_EmitArrayEntries_AllConditional ()
		{
			// 2-arg unconditional makes no sense for arrays — the trim conditioning on the
			// array shape is the whole point.
			var peer = MakeMcwPeer ("foo/Bar", "Foo.Bar", "App");
			var model = BuildModelWithArrays (new [] { peer });

			foreach (var entry in model.Entries.Where (e => e.AnchorRank is not null)) {
				Assert.False (entry.IsUnconditional);
				Assert.NotNull (entry.TargetTypeReference);
			}
		}

		[Fact]
		public void Build_EmitArrayEntries_OpenGenericPeer_Skipped ()
		{
			// typeof(JavaList<>[]) is not a valid IL token.
			var openGeneric = MakeMcwPeer ("java/util/ArrayList", "Android.Runtime.JavaList`1", "Mono.Android")
				with { IsGenericDefinition = true };
			var model = BuildModelWithArrays (new [] { openGeneric });

			Assert.DoesNotContain (model.Entries, e => e.AnchorRank is not null);
		}

		[Fact]
		public void Build_EmitArrayEntries_AliasGroup_Skipped ()
		{
			// Alias groups (multiple peers sharing one JNI name) would produce duplicate
			// JNI array keys; deferred pending an alias-aware design.
			var peers = new List<JavaPeerInfo> {
				MakeMcwPeer ("test/Dup", "Test.First", "App"),
				MakeMcwPeer ("test/Dup", "Test.Second", "App"),
			};
			var model = BuildModelWithArrays (peers);

			Assert.DoesNotContain (model.Entries, e => e.AnchorRank is not null);
		}

		[Theory]
		[InlineData ("Z")]
		[InlineData ("B")]
		[InlineData ("C")]
		[InlineData ("S")]
		[InlineData ("I")]
		[InlineData ("J")]
		[InlineData ("F")]
		[InlineData ("D")]
		public void Build_EmitArrayEntries_PrimitiveJniKeyword_Skipped (string jniKeyword)
		{
			// Primitive JNI keyword keys are handled by the legacy
			// JniRuntime.JniTypeManager.GetPrimitiveArrayTypesForSimpleReference path.
			// Emitting array entries here would shadow that built-in handling.
			var peer = MakeMcwPeer (jniKeyword, "FakePrimitive.Wrapper", "App");
			var model = BuildModelWithArrays (new [] { peer });

			Assert.DoesNotContain (model.Entries, e => e.AnchorRank is not null);
		}

		[Fact]
		public void Build_EmitArrayEntries_MultiplePeers_GetIndependentTrios ()
		{
			var peers = new List<JavaPeerInfo> {
				MakeMcwPeer ("foo/A", "Foo.A", "App"),
				MakeMcwPeer ("foo/B", "Foo.B", "App"),
			};
			var model = BuildModelWithArrays (peers);

			var arrayEntries = model.Entries.Where (e => e.AnchorRank is not null).ToList ();
			Assert.Equal (6, arrayEntries.Count);   // 2 peers × 3 ranks

			foreach (var jni in new [] { "foo/A", "foo/B" }) {
				var perPeer = arrayEntries.Where (e => e.JniName == jni).OrderBy (e => e.AnchorRank).ToList ();
				Assert.Equal (3, perPeer.Count);
				Assert.Equal (new int? [] { 1, 2, 3 }, perPeer.Select (e => e.AnchorRank).ToArray ());
			}
		}
	}

	public class ArrayEntriesPeBlob
	{
		[Fact]
		public void FullPipeline_ArrayEntries_ReferencesSharedRankAnchors ()
		{
			var peer = MakeMcwPeer ("foo/Bar", "Foo.Bar", "App");
			var outputPath = Path.Combine (Path.GetTempPath (), "ArrSentinels.dll");
			var model = ModelBuilder.Build (new [] { peer }, outputPath, "ArrSentinels", maxArrayRank: 3);
			Assert.Equal (3, model.MaxArrayRank);

			EmitAndVerify (model, "ArrSentinels", (pe, reader) => {
				// Per-asm DLLs no longer define their own __ArrayMapRank{N}; they reference
				// the shared anchors in Mono.Android.
				var typeDefNames = reader.TypeDefinitions
					.Select (h => reader.GetString (reader.GetTypeDefinition (h).Name))
					.ToHashSet (StringComparer.Ordinal);
				Assert.DoesNotContain ("__ArrayMapRank1", typeDefNames);

				var rankRefsToMonoAndroid = reader.TypeReferences
					.Select (h => reader.GetTypeReference (h))
					.Where (t => reader.GetString (t.Name).StartsWith ("__ArrayMapRank", StringComparison.Ordinal))
					.Select (t => reader.GetString (t.Name))
					.ToHashSet (StringComparer.Ordinal);
				Assert.Contains ("__ArrayMapRank1", rankRefsToMonoAndroid);
				Assert.Contains ("__ArrayMapRank2", rankRefsToMonoAndroid);
				Assert.Contains ("__ArrayMapRank3", rankRefsToMonoAndroid);
			});
		}

		[Fact]
		public void FullPipeline_NoArrayEntries_DoesNotReferenceRankAnchors ()
		{
			var peer = MakeMcwPeer ("foo/Bar", "Foo.Bar", "App");
			var outputPath = Path.Combine (Path.GetTempPath (), "NoArrSentinels.dll");
			var model = ModelBuilder.Build (new [] { peer }, outputPath, "NoArrSentinels");
			Assert.Equal (0, model.MaxArrayRank);

			EmitAndVerify (model, "NoArrSentinels", (pe, reader) => {
				var typeRefNames = reader.TypeReferences
					.Select (h => reader.GetString (reader.GetTypeReference (h).Name))
					.ToHashSet (StringComparer.Ordinal);
				Assert.DoesNotContain ("__ArrayMapRank1", typeRefNames);
				Assert.DoesNotContain ("__ArrayMapRank2", typeRefNames);
				Assert.DoesNotContain ("__ArrayMapRank3", typeRefNames);
			});
		}

		[Fact]
		public void FullPipeline_ArrayEntries_AttributeBlobsRoundTrip ()
		{
			var peer = MakeMcwPeer ("foo/Bar", "Foo.Bar", "App");
			var outputPath = Path.Combine (Path.GetTempPath (), "ArrBlobs.dll");
			var model = ModelBuilder.Build (new [] { peer }, outputPath, "ArrBlobs", maxArrayRank: 3);

			EmitAndVerify (model, "ArrBlobs", (pe, reader) => {
				var attrs = ReadAllTypeMapAttributeBlobs (reader);

				// Three array entries should round-trip with the same JNI key + array trim targets.
				Assert.Contains (attrs, a => a.jniName == "foo/Bar" && a.targetRef == "Foo.Bar[], App");
				Assert.Contains (attrs, a => a.jniName == "foo/Bar" && a.targetRef == "Foo.Bar[][], App");
				Assert.Contains (attrs, a => a.jniName == "foo/Bar" && a.targetRef == "Foo.Bar[][][], App");
			});
		}
	}

	static void EmitAndVerify (TypeMapAssemblyData model, string assemblyName, Action<PEReader, MetadataReader> verify)
	{
		var stream = new MemoryStream ();
		var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
		emitter.Emit (model, stream);
		stream.Position = 0;
		using var pe = new PEReader (stream);
		verify (pe, pe.GetMetadataReader ());
	}

	/// <summary>
	/// Reads the first TypeMap assembly-level attribute blob and returns (jniName, proxyRef, targetRef).
	/// targetRef is null for 2-arg attributes.
	/// </summary>
	static (string? jniName, string? proxyRef, string? targetRef) ReadFirstTypeMapAttributeBlob (MetadataReader reader)
	{
		var all = ReadAllTypeMapAttributeBlobs (reader);
		if (all.Count == 0) {
			throw new InvalidOperationException ("No TypeMap attribute found on assembly");
		}
		return all [0];
	}

	/// <summary>
	/// Reads TypeMap attribute blobs from a PE assembly's metadata.
	///
	/// NOTE: This is a PE-level integration test helper, not a primary unit test mechanism.
	/// The model-level tests (which verify TypeMapAssemblyData directly) are the main unit tests.
	/// These PE round-trip tests exist to catch encoding bugs in the emitter and to verify that
	/// the full scan→model→emit pipeline produces a valid, loadable assembly.
	///
	/// The distinction between TypeMap and IgnoresAccessChecksTo attributes relies on
	/// attr.Constructor.Kind: TypeMap attributes reference their ctor via MemberReference
	/// (because the attribute type is a TypeSpec — generic), while IgnoresAccessChecksTo
	/// uses MethodDefinition (the attribute type is defined in the same assembly as a TypeDef).
	/// If this logic breaks, the test will either fail to find TypeMap attributes or
	/// misidentify IgnoresAccessChecksTo as TypeMap — both cause obvious assertion failures.
	/// </summary>
	static List<(string? jniName, string? proxyRef, string? targetRef)> ReadAllTypeMapAttributeBlobs (MetadataReader reader)
	{
		var result = new List<(string?, string?, string?)> ();
		var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
		foreach (var attrHandle in asmAttrs) {
			var attr = reader.GetCustomAttribute (attrHandle);
			if (attr.Constructor.Kind != HandleKind.MemberReference)
				continue;

			var ctor = reader.GetMemberReference ((MemberReferenceHandle) attr.Constructor);
			if (ctor.Parent.Kind != HandleKind.TypeSpecification)
				continue;

			var blobReader = reader.GetBlobReader (attr.Value);
			ushort prolog = blobReader.ReadUInt16 ();
			if (prolog != 1)
				continue;

			string? jniName = blobReader.ReadSerializedString ();
			string? proxyRef = blobReader.ReadSerializedString ();

			// Try to read third arg (target type) — if remaining bytes are just NumNamed (2 bytes), it's 2-arg
			string? targetRef = null;
			if (blobReader.RemainingBytes > 2) {
				targetRef = blobReader.ReadSerializedString ();
			}

			if (string.IsNullOrEmpty (jniName) || !jniName.Contains ('/')) {
				continue;
			}

			result.Add ((jniName, proxyRef, targetRef));
		}
		return result;
	}

	public class UcoMethods
	{
		[Fact]
		public void Build_AcwWithMarshalMethods_CreatesUcoMethods ()
		{
			var peer = MakeAcwPeer ("my/app/Foo", "MyApp.Foo", "App") with {
				MarshalMethods = new List<MarshalMethodInfo> {
					new MarshalMethodInfo {
						JniName = "<init>", NativeCallbackName = "n_ctor",
						JniSignature = "()V", ManagedMethodName = ".ctor",
						IsConstructor = true,
					},
					new MarshalMethodInfo {
						JniName = "onClick", NativeCallbackName = "n_OnClick",
						JniSignature = "(Landroid/view/View;)V", ManagedMethodName = "OnClick",
					},
				},
			};
			var model = BuildModel (new [] { peer });

			Assert.Single (model.ProxyTypes);
			var proxy = model.ProxyTypes [0];
			Assert.True (proxy.IsAcw);
			// Only non-constructor methods become UCO methods
			Assert.Single (proxy.UcoMethods);
			Assert.Equal ("n_OnClick", proxy.UcoMethods [0].CallbackMethodName);
		}

		[Fact]
		public void Build_ConstructorsInMarshalMethods_SkippedFromUcoMethods ()
		{
			var peer = MakeAcwPeer ("my/app/Bar", "MyApp.Bar", "App") with {
				MarshalMethods = new List<MarshalMethodInfo> {
					new MarshalMethodInfo {
						JniName = "<init>", NativeCallbackName = "n_ctor",
						JniSignature = "()V", ManagedMethodName = ".ctor",
						IsConstructor = true,
					},
				},
			};
			var model = BuildModel (new [] { peer });
			Assert.Empty (model.ProxyTypes [0].UcoMethods);
		}

		[Fact]
		public void Build_McwType_IsAcwFalse ()
		{
			var peer = MakePeerWithActivation ("android/app/Activity", "Android.App.Activity", "Mono.Android");
			var model = BuildModel (new [] { peer });
			Assert.Single (model.ProxyTypes);
			Assert.False (model.ProxyTypes [0].IsAcw);
		}

		[Fact]
		public void Build_InterfaceWithMarshalMethods_IsNotAcw ()
		{
			var peer = MakeInterfacePeer ("android/view/View$OnClickListener",
				"Android.Views.View+IOnClickListener", "Mono.Android",
				"Android.Views.View+IOnClickListenerInvoker") with {
				MarshalMethods = new List<MarshalMethodInfo> {
					new MarshalMethodInfo {
						JniName = "onClick", NativeCallbackName = "n_OnClick",
						JniSignature = "(Landroid/view/View;)V", ManagedMethodName = "OnClick",
					},
				},
			};
			var model = BuildModel (new [] { peer });
			Assert.Single (model.ProxyTypes);
			Assert.False (model.ProxyTypes [0].IsAcw);
		}
	}

	public class UcoConstructors
	{
		[Fact]
		public void Build_AcwWithConstructors_CreatesUcoConstructors ()
		{
			var peer = MakeAcwPeer ("my/app/Baz", "MyApp.Baz", "App") with {
				JavaConstructors = new List<JavaConstructorInfo> {
					new JavaConstructorInfo { ConstructorIndex = 0, JniSignature = "()V" },
					new JavaConstructorInfo { ConstructorIndex = 1, JniSignature = "(Landroid/content/Context;)V" },
				},
			};
			var model = BuildModel (new [] { peer });
			Assert.Equal (2, model.ProxyTypes [0].UcoConstructors.Count);
			Assert.Contains ("nctor_0_uco", model.ProxyTypes [0].UcoConstructors [0].WrapperName);
			Assert.Contains ("nctor_1_uco", model.ProxyTypes [0].UcoConstructors [1].WrapperName);
		}

		[Fact]
		public void Build_PeerWithoutActivationCtor_NoUcoConstructors ()
		{
			var peer = MakeMcwPeer ("test/NoActivation", "Test.NoActivation", "Asm");
			var model = BuildModel (new [] { peer });
			Assert.Empty (model.ProxyTypes);
		}
	}

	public class NativeRegistrations
	{
		[Fact]
		public void Build_NativeRegistrations_MatchUcoMethods ()
		{
			var peer = MakeAcwPeer ("my/app/Reg", "MyApp.Reg", "App") with {
				MarshalMethods = new List<MarshalMethodInfo> {
					new MarshalMethodInfo {
						JniName = "<init>", NativeCallbackName = "n_ctor",
						JniSignature = "()V", ManagedMethodName = ".ctor",
						IsConstructor = true,
					},
					new MarshalMethodInfo {
						JniName = "doWork", NativeCallbackName = "n_DoWork",
						JniSignature = "(I)V", ManagedMethodName = "DoWork",
					},
				},
				JavaConstructors = new List<JavaConstructorInfo> {
					new JavaConstructorInfo { ConstructorIndex = 0, JniSignature = "()V" },
				},
			};
			var model = BuildModel (new [] { peer });
			var proxy = model.ProxyTypes [0];

			// Should have 1 UCO method + 1 UCO constructor = 2 native registrations
			Assert.Single (proxy.UcoMethods);
			Assert.Single (proxy.UcoConstructors);
			Assert.Equal (2, proxy.NativeRegistrations.Count);
		}

		[Fact]
		public void Build_NonAcwProxy_NoNativeRegistrations ()
		{
			var peer = MakePeerWithActivation ("test/Mcw", "Test.Mcw", "Mono.Android");
			var model = BuildModel (new [] { peer });
			Assert.Single (model.ProxyTypes);
			Assert.Empty (model.ProxyTypes [0].NativeRegistrations);
		}
	}

	public class FixtureUcoMethods
	{
		[Fact]
		public void Fixture_MainActivity_UcoMethods ()
		{
			var peer = FindFixtureByJavaName ("my/app/MainActivity");
			var model = BuildModel (new [] { peer }, "TypeMap");
			var proxy = model.ProxyTypes.FirstOrDefault ();
			Assert.NotNull (proxy);
			Assert.True (proxy.IsAcw);
			Assert.NotEmpty (proxy.UcoMethods);
		}

		[Fact]
		public void Fixture_ClickableView_HasOnClickUcoWrapper ()
		{
			var peer = FindFixtureByJavaName ("my/app/ClickableView");
			var model = BuildModel (new [] { peer }, "TypeMap");
			var proxy = model.ProxyTypes.FirstOrDefault ();
			Assert.NotNull (proxy);
			var ucoNames = proxy.UcoMethods.Select (u => u.CallbackMethodName).ToList ();
			Assert.Contains ("n_OnClick", ucoNames);
		}

		[Fact]
		public void Fixture_TouchHandler_AllUcoMethods ()
		{
			var peer = FindFixtureByJavaName ("my/app/TouchHandler");
			var model = BuildModel (new [] { peer }, "TypeMap");
			var proxy = model.ProxyTypes.FirstOrDefault ();
			Assert.NotNull (proxy);
			Assert.True (proxy.UcoMethods.Count >= 2, "TouchHandler should have multiple UCO methods");
		}

		[Fact]
		public void Fixture_CustomView_HasTwoConstructorWrappers ()
		{
			var peer = FindFixtureByJavaName ("my/app/CustomView");
			var model = BuildModel (new [] { peer }, "TypeMap");
			var proxy = model.ProxyTypes.FirstOrDefault ();
			Assert.NotNull (proxy);
			Assert.Equal (2, proxy.UcoConstructors.Count);
		}
	}
}
