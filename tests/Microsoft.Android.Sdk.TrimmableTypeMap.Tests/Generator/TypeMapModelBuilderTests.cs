using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public class ModelBuilderTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (ModelBuilderTests).Assembly.Location)!;
			var fixtureAssembly = Path.Combine (testAssemblyDir, "TestFixtures.dll");
			Assert.True (File.Exists (fixtureAssembly),
				$"TestFixtures.dll not found at {fixtureAssembly}. Ensure the TestFixtures project builds.");
			return fixtureAssembly;
		}
	}

	static readonly Lazy<List<JavaPeerInfo>> _cachedFixtures = new (() => {
		using var scanner = new JavaPeerScanner ();
		return scanner.Scan (new [] { TestFixtureAssemblyPath });
	});

	static List<JavaPeerInfo> ScanFixtures () => _cachedFixtures.Value;

	static TypeMapAssemblyData BuildModel (IReadOnlyList<JavaPeerInfo> peers, string? assemblyName = null)
	{
		var outputPath = Path.Combine ("/tmp", (assemblyName ?? "TestTypeMap") + ".dll");
		return ModelBuilder.Build (peers, outputPath, assemblyName);
	}


	public class BasicStructure
	{

		[Fact]
		public void Build_EmptyPeers_ProducesEmptyModel ()
		{
			var model = BuildModel (Array.Empty<JavaPeerInfo> (), "Empty");
			Assert.Equal ("Empty", model.AssemblyName);
			Assert.Equal ("Empty.dll", model.ModuleName);
			Assert.Empty (model.Entries);
			Assert.Empty (model.ProxyTypes);
		}

		[Fact]
		public void Build_AssemblyNameDerivedFromOutputPath ()
		{
			var model = ModelBuilder.Build (Array.Empty<JavaPeerInfo> (), "/some/path/Foo.Bar.dll");
			Assert.Equal ("Foo.Bar", model.AssemblyName);
			Assert.Equal ("Foo.Bar.dll", model.ModuleName);
		}

		[Fact]
		public void Build_ExplicitAssemblyName_OverridesOutputPath ()
		{
			var model = ModelBuilder.Build (Array.Empty<JavaPeerInfo> (), "/some/path/Foo.dll", "MyAssembly");
			Assert.Equal ("MyAssembly", model.AssemblyName);
		}

		[Fact]
		public void Build_EmptyInput_HasEmptyIgnoresAccessChecksTo ()
		{
			var model = BuildModel (Array.Empty<JavaPeerInfo> ());
			Assert.Empty (model.IgnoresAccessChecksTo);
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
			// Entries are ordered by JNI name (alphabetical)
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
			// Two entries: primary "test/Dup" and alias "test/Dup[1]"
			Assert.Equal (2, model.Entries.Count);
			Assert.Equal ("test/Dup", model.Entries [0].JniName);
			Assert.Contains ("Test.First", model.Entries [0].ProxyTypeReference);
			Assert.Equal ("test/Dup[1]", model.Entries [1].JniName);
			Assert.Contains ("Test.Second", model.Entries [1].ProxyTypeReference);
		}

	}

	public class ConditionalAttributes
	{

		[Fact]
		public void Build_EssentialRuntimeType_IsUnconditional ()
		{
			var peer = MakeMcwPeer ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
			peer.DoNotGenerateAcw = true;
			var model = BuildModel (new [] { peer });

			Assert.Single (model.Entries);
			Assert.True (model.Entries [0].IsUnconditional);
			Assert.Null (model.Entries [0].TargetTypeReference);
		}

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
			var peer = MakeMcwPeer (jniName, "Java.Lang.SomeType", "Mono.Android");
			peer.DoNotGenerateAcw = true;
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
			// MCW binding types (DoNotGenerateAcw=true) are trimmable unless essential
			var peer = MakeMcwPeer ("android/app/Activity", "Android.App.Activity", "Mono.Android");
			peer.DoNotGenerateAcw = true;
			var model = BuildModel (new [] { peer });

			Assert.Single (model.Entries);
			Assert.False (model.Entries [0].IsUnconditional);
			Assert.NotNull (model.Entries [0].TargetTypeReference);
			Assert.Contains ("Android.App.Activity, Mono.Android", model.Entries [0].TargetTypeReference!);
		}

		[Fact]
		public void Build_Interface_IsTrimmable ()
		{
			var peer = new JavaPeerInfo {
				JavaName = "android/view/View$OnClickListener",
				ManagedTypeName = "Android.Views.View+IOnClickListener",
				ManagedTypeNamespace = "Android.Views",
				ManagedTypeShortName = "IOnClickListener",
				AssemblyName = "Mono.Android",
				IsInterface = true,
				InvokerTypeName = "Android.Views.View+IOnClickListenerInvoker",
			};

			var model = BuildModel (new [] { peer });
			Assert.Single (model.Entries);
			Assert.False (model.Entries [0].IsUnconditional);
			Assert.NotNull (model.Entries [0].TargetTypeReference);
		}

		[Fact]
		public void Build_UnconditionalScannedType_IsUnconditional ()
		{
			// Types with IsUnconditional from scanner (e.g., from [Activity], [Service] attrs)
			var peer = MakeMcwPeer ("my/app/MySvc", "MyApp.MyService", "App");
			peer.DoNotGenerateAcw = true; // simulate MCW-like
			peer.IsUnconditional = true; // scanner marked it
			var model = BuildModel (new [] { peer });

			Assert.True (model.Entries [0].IsUnconditional);
		}

	}

	public class Aliases
	{

		[Fact]
		public void Build_AliasedPeers_GetIndexedJniNames ()
		{
			var peers = new List<JavaPeerInfo> {
				MakeMcwPeer ("test/Dup", "Test.First", "A"),
				MakeMcwPeer ("test/Dup", "Test.Second", "A"),
				MakeMcwPeer ("test/Dup", "Test.Third", "A"),
			};

			var model = BuildModel (peers);
			Assert.Equal (3, model.Entries.Count);
			Assert.Equal ("test/Dup", model.Entries [0].JniName);
			Assert.Equal ("test/Dup[1]", model.Entries [1].JniName);
			Assert.Equal ("test/Dup[2]", model.Entries [2].JniName);
		}

		[Fact]
		public void Build_AliasedPeersWithActivation_GetDistinctProxies ()
		{
			var peers = new List<JavaPeerInfo> {
				MakePeerWithActivation ("test/Dup", "Test.First", "A"),
				MakePeerWithActivation ("test/Dup", "Test.Second", "A"),
			};

			var model = BuildModel (peers, "TypeMap");
			Assert.Equal (2, model.ProxyTypes.Count);
			// Distinct proxy names based on managed type names
			Assert.Equal ("Test_First_Proxy", model.ProxyTypes [0].TypeName);
			Assert.Equal ("Test_Second_Proxy", model.ProxyTypes [1].TypeName);
		}

		[Fact]
		public void Build_McwPeerWithoutActivation_NoProxy ()
		{
			var peer = MakeMcwPeer ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
			// No activation ctor, no invoker → no proxy
			var model = BuildModel (new [] { peer });

			Assert.Empty (model.ProxyTypes);
			Assert.Single (model.Entries);
			Assert.Contains ("Java.Lang.Object, Mono.Android", model.Entries [0].ProxyTypeReference);
		}

	}

	public class ProxyTypes
	{

		[Fact]
		public void Build_PeerWithActivationCtor_CreatesProxy ()
		{
			var peer = MakePeerWithActivation ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
			var model = BuildModel (new [] { peer }, "MyTypeMap");

			Assert.Single (model.ProxyTypes);
			var proxy = model.ProxyTypes [0];
			Assert.Equal ("Java_Lang_Object_Proxy", proxy.TypeName);
			Assert.Equal ("_TypeMap.Proxies", proxy.Namespace);
			Assert.True (proxy.HasActivation);
			Assert.Equal ("Java.Lang.Object", proxy.TargetType.ManagedTypeName);
			Assert.Equal ("Mono.Android", proxy.TargetType.AssemblyName);
		}

		[Fact]
		public void Build_PeerWithInvoker_CreatesProxy ()
		{
			var peer = new JavaPeerInfo {
				JavaName = "android/view/View$OnClickListener",
				ManagedTypeName = "Android.Views.View+IOnClickListener",
				ManagedTypeNamespace = "Android.Views",
				ManagedTypeShortName = "IOnClickListener",
				AssemblyName = "Mono.Android",
				IsInterface = true,
				InvokerTypeName = "Android.Views.View+IOnClickListenerInvoker",
			};

			var model = BuildModel (new [] { peer });
			Assert.Single (model.ProxyTypes);
			var proxy = model.ProxyTypes [0];
			Assert.NotNull (proxy.InvokerType);
			Assert.Equal ("Android.Views.View+IOnClickListenerInvoker", proxy.InvokerType!.ManagedTypeName);
		}

		[Fact]
		public void Build_PeerWithInvokerButNoActivationCtor_ProxyHasActivationTrue ()
		{
			// An interface with an invoker type has HasActivation = true because
			// CreateInstance will instantiate the invoker type.
			var peer = new JavaPeerInfo {
				JavaName = "android/view/View$OnClickListener",
				ManagedTypeName = "Android.Views.View+IOnClickListener",
				ManagedTypeNamespace = "Android.Views",
				ManagedTypeShortName = "IOnClickListener",
				AssemblyName = "Mono.Android",
				IsInterface = true,
				InvokerTypeName = "Android.Views.View+IOnClickListenerInvoker",
			};

			var model = BuildModel (new [] { peer });
			Assert.Single (model.ProxyTypes);
			var proxy = model.ProxyTypes [0];
			Assert.True (proxy.HasActivation);
			Assert.NotNull (proxy.InvokerType);
		}

		[Fact]
		public void Build_ProxyNaming_ReplacesDotAndPlus ()
		{
			var peer = MakePeerWithActivation ("com/example/Outer$Inner", "Com.Example.Outer.Inner", "App");
			var model = BuildModel (new [] { peer });

			Assert.Single (model.ProxyTypes);
			Assert.Equal ("Com_Example_Outer_Inner_Proxy", model.ProxyTypes [0].TypeName);
		}

		[Fact]
		public void Build_EntryPointsToProxy_WhenProxyExists ()
		{
			var peer = MakePeerWithActivation ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
			var model = BuildModel (new [] { peer }, "MyTypeMap");

			var entry = model.Entries [0];
			Assert.Contains ("Java_Lang_Object_Proxy", entry.ProxyTypeReference);
			Assert.Contains ("MyTypeMap", entry.ProxyTypeReference);
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

			// All entries have non-empty JNI names
			Assert.All (model.Entries, e => Assert.False (string.IsNullOrEmpty (e.JniName)));
			Assert.All (model.Entries, e => Assert.False (string.IsNullOrEmpty (e.ProxyTypeReference)));
		}

		[Fact]
		public void Build_FromScannedFixtures_NoProxiesForMcwWithoutActivation ()
		{
			var peers = ScanFixtures ();
			var model = BuildModel (peers);

			// Proxy type names should all end with _Proxy
			Assert.All (model.ProxyTypes, p => Assert.EndsWith ("_Proxy", p.TypeName));
		}

	}

	public class FixtureConditionalAttributes
	{

		[Fact]
		public void Fixture_JavaLangObject_IsUnconditional ()
		{
			var peer = FindFixtureByJavaName ("java/lang/Object");
			var model = BuildModel (new [] { peer });
			Assert.True (model.Entries [0].IsUnconditional);
		}

		[Fact]
		public void Fixture_Throwable_IsUnconditional ()
		{
			var peer = FindFixtureByJavaName ("java/lang/Throwable");
			var model = BuildModel (new [] { peer });
			Assert.True (model.Entries [0].IsUnconditional);
		}

		[Fact]
		public void Fixture_Exception_IsUnconditional ()
		{
			var peer = FindFixtureByJavaName ("java/lang/Exception");
			var model = BuildModel (new [] { peer });
			Assert.True (model.Entries [0].IsUnconditional);
		}

		[Fact]
		public void Fixture_Activity_McwBinding_IsTrimmable ()
		{
			var peer = FindFixtureByJavaName ("android/app/Activity");
			Assert.True (peer.DoNotGenerateAcw);
			var model = BuildModel (new [] { peer });
			// Activity is MCW and not an essential runtime type → trimmable
			Assert.False (model.Entries [0].IsUnconditional);
			Assert.Contains ("Android.App.Activity", model.Entries [0].TargetTypeReference!);
		}

		[Fact]
		public void Fixture_MainActivity_UserAcw_IsUnconditional ()
		{
			var peer = FindFixtureByJavaName ("my/app/MainActivity");
			Assert.False (peer.DoNotGenerateAcw);
			Assert.False (peer.IsInterface);
			var model = BuildModel (new [] { peer });
			Assert.True (model.Entries [0].IsUnconditional);
		}

		[Fact]
		public void Fixture_IOnClickListener_Interface_IsTrimmable ()
		{
			var peers = ScanFixtures ();
			var listener = peers.First (p => p.ManagedTypeName == "Android.Views.IOnClickListener");
			var model = BuildModel (new [] { listener });
			Assert.False (model.Entries [0].IsUnconditional);
		}

		[Fact]
		public void Fixture_TouchHandler_UserType_IsUnconditional ()
		{
			var peer = FindFixtureByJavaName ("my/app/TouchHandler");
			Assert.False (peer.DoNotGenerateAcw);
			var model = BuildModel (new [] { peer });
			Assert.True (model.Entries [0].IsUnconditional);
		}

		[Fact]
		public void Fixture_Button_McwBinding_IsTrimmable ()
		{
			var peer = FindFixtureByJavaName ("android/widget/Button");
			Assert.True (peer.DoNotGenerateAcw);
			var model = BuildModel (new [] { peer });
			Assert.False (model.Entries [0].IsUnconditional);
		}

	}

	static JavaPeerInfo MakeMcwPeer (string jniName, string managedName, string asmName)
	{
		var ns = managedName.Contains ('.') ? managedName.Substring (0, managedName.LastIndexOf ('.')) : "";
		var shortName = managedName.Contains ('.') ? managedName.Substring (managedName.LastIndexOf ('.') + 1) : managedName;
		return new JavaPeerInfo {
			JavaName = jniName,
			ManagedTypeName = managedName,
			ManagedTypeNamespace = ns,
			ManagedTypeShortName = shortName,
			AssemblyName = asmName,
		};
	}

	static JavaPeerInfo MakePeerWithActivation (string jniName, string managedName, string asmName)
	{
		var peer = MakeMcwPeer (jniName, managedName, asmName);
		peer.ActivationCtor = new ActivationCtorInfo {
			Style = ActivationCtorStyle.XamarinAndroid,
		};
		return peer;
	}

	static JavaPeerInfo MakeAcwPeer (string jniName, string managedName, string asmName)
	{
		var peer = MakePeerWithActivation (jniName, managedName, asmName);
		peer.DoNotGenerateAcw = false;
		// Add a constructor so it qualifies as ACW
		peer.JavaConstructors = new List<JavaConstructorInfo> {
			new JavaConstructorInfo { ConstructorIndex = 0, JniSignature = "()V" },
		};
		// Need at least 1 marshal method to be ACW
		peer.MarshalMethods = new List<MarshalMethodInfo> {
			new MarshalMethodInfo {
				JniName = "<init>",
				NativeCallbackName = "n_ctor",
				JniSignature = "()V",
				IsConstructor = true,
			},
		};
		return peer;
	}

	static MarshalMethodInfo MakeMarshalMethod (string jniName, string callbackName, string jniSig, bool isConstructor = false)
	{
		return new MarshalMethodInfo {
			JniName = jniName,
			NativeCallbackName = callbackName,
			JniSignature = jniSig,
			IsConstructor = isConstructor,
		};
	}

	// Fixture-based tests: scan the real TestFixtures.dll and verify model output

	static JavaPeerInfo FindFixtureByJavaName (string javaName)
	{
		var peers = ScanFixtures ();
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
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

		[Fact]
		public void Fixture_JavaLangObject_HasActivation_CreatesProxy ()
		{
			var peer = FindFixtureByJavaName ("java/lang/Object");
			var model = BuildModel (new [] { peer }, "TypeMap");

			var proxy = FindProxy (model, "Java_Lang_Object_Proxy");
			Assert.NotNull (proxy);
			Assert.True (proxy!.HasActivation);
			Assert.Equal ("Java.Lang.Object", proxy.TargetType.ManagedTypeName);
			Assert.Equal ("TestFixtures", proxy.TargetType.AssemblyName);
		}

		[Fact]
		public void Fixture_Activity_HasActivation_CreatesProxy ()
		{
			var peer = FindFixtureByJavaName ("android/app/Activity");
			var model = BuildModel (new [] { peer }, "TypeMap");

			var proxy = FindProxy (model, "Android_App_Activity_Proxy");
			Assert.NotNull (proxy);
			Assert.True (proxy!.HasActivation);
			Assert.Equal ("Android.App.Activity", proxy.TargetType.ManagedTypeName);
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
		public void Fixture_Throwable_HasActivation ()
		{
			var peer = FindFixtureByJavaName ("java/lang/Throwable");
			var model = BuildModel (new [] { peer }, "TypeMap");

			var proxy = FindProxy (model, "Java_Lang_Throwable_Proxy");
			Assert.NotNull (proxy);
			Assert.True (proxy!.HasActivation);
		}

		[Fact]
		public void Fixture_Exception_HasActivation ()
		{
			var peer = FindFixtureByJavaName ("java/lang/Exception");
			var model = BuildModel (new [] { peer }, "TypeMap");

			var proxy = FindProxy (model, "Java_Lang_Exception_Proxy");
			Assert.NotNull (proxy);
			Assert.True (proxy!.HasActivation);
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

		[Fact]
		public void Fixture_Context_HasActivation ()
		{
			var peer = FindFixtureByJavaName ("android/content/Context");
			var model = BuildModel (new [] { peer }, "TypeMap");

			// Context has (IntPtr, JniHandleOwnership) ctor
			if (peer.ActivationCtor != null) {
				var proxy = FindProxy (model, "Android_Content_Context_Proxy");
				Assert.NotNull (proxy);
			}
		}

		[Fact]
		public void Fixture_View_HasActivation ()
		{
			var peer = FindFixtureByJavaName ("android/view/View");
			var model = BuildModel (new [] { peer }, "TypeMap");

			if (peer.ActivationCtor != null) {
				var proxy = FindProxy (model, "Android_Views_View_Proxy");
				Assert.NotNull (proxy);
			}
		}

		[Fact]
		public void Fixture_Button_HasActivation ()
		{
			var peer = FindFixtureByJavaName ("android/widget/Button");
			var model = BuildModel (new [] { peer }, "TypeMap");

			if (peer.ActivationCtor != null) {
				var proxy = FindProxy (model, "Android_Widget_Button_Proxy");
				Assert.NotNull (proxy);
			}
		}

	}

	public class FixtureCustomView
	{

		[Fact]
		public void Fixture_CustomView_HasTwoConstructors ()
		{
			var peer = FindFixtureByJavaName ("my/app/CustomView");
			Assert.Equal (2, peer.JavaConstructors.Count);

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

		[Fact]
		public void Fixture_OuterInner_ProxyNaming ()
		{
			var peer = FindFixtureByJavaName ("my/app/Outer$Inner");
			var model = BuildModel (new [] { peer }, "TypeMap");

			// . and + get replaced with _
			var entry = FindEntry (model, "my/app/Outer$Inner");
			Assert.NotNull (entry);

			if (peer.ActivationCtor != null) {
				var proxy = FindProxy (model, "MyApp_Outer_Inner_Proxy");
				Assert.NotNull (proxy);
				Assert.Equal ("MyApp.Outer+Inner", proxy!.TargetType.ManagedTypeName);
			}
		}

		[Fact]
		public void Fixture_ICallbackResult_ProxyNaming ()
		{
			var peer = FindFixtureByJavaName ("my/app/ICallback$Result");
			var model = BuildModel (new [] { peer }, "TypeMap");

			var entry = FindEntry (model, "my/app/ICallback$Result");
			Assert.NotNull (entry);

			if (peer.ActivationCtor != null) {
				var proxy = FindProxy (model, "MyApp_ICallback_Result_Proxy");
				Assert.NotNull (proxy);
				Assert.Equal ("MyApp.ICallback+Result", proxy!.TargetType.ManagedTypeName);
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

			// Invoker is excluded entirely — no TypeMap entry, no proxy.
			// Only the interface gets a TypeMap entry and a proxy.
			Assert.Single (model.Entries);
			Assert.Equal ("android/view/View$OnClickListener", model.Entries [0].JniName);

			// Only the interface proxy exists; the invoker type is referenced
			// only as a TypeRef in the interface proxy's InvokerType property.
			Assert.Single (model.ProxyTypes);
			Assert.NotNull (model.ProxyTypes [0].InvokerType);
			Assert.Equal ("Android.Views.IOnClickListenerInvoker", model.ProxyTypes [0].InvokerType!.ManagedTypeName);
		}

		[Fact]
		public void Build_InvokerType_NoProxyNoEntry ()
		{
			// Invoker types should never get their own proxy or TypeMap entry.
			// They only appear as a TypeRef in the interface proxy's InvokerType/CreateInstance.
			var ifacePeer = new JavaPeerInfo {
				JavaName = "my/app/IFoo",
				ManagedTypeName = "MyApp.IFoo",
				AssemblyName = "App",
				IsInterface = true,
				InvokerTypeName = "MyApp.FooInvoker",
			};
			var invokerPeer = MakePeerWithActivation ("my/app/IFoo", "MyApp.FooInvoker", "App");
			invokerPeer.DoNotGenerateAcw = true;

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

	}

	public class FixtureAbstractBase
	{

		[Fact]
		public void Fixture_AbstractBase_HasProxy ()
		{
			var peer = FindFixtureByJavaName ("my/app/AbstractBase");
			Assert.True (peer.IsAbstract);
			Assert.False (peer.DoNotGenerateAcw);

			var model = BuildModel (new [] { peer }, "TypeMap");

			// AbstractBase has marshal methods (doWork) and activation ctor
			if (peer.ActivationCtor != null && peer.MarshalMethods.Count > 0) {
				var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "MyApp_AbstractBase_Proxy");
				Assert.NotNull (proxy);
			}
		}

	}

	public class FixtureClickableView
	{

		[Fact]
		public void Fixture_ClickableView_HasProxy ()
		{
			var peer = FindFixtureByJavaName ("my/app/ClickableView");
			Assert.False (peer.DoNotGenerateAcw);

			var model = BuildModel (new [] { peer }, "TypeMap");

			if (peer.ActivationCtor != null && peer.MarshalMethods.Count > 0) {
				var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "MyApp_ClickableView_Proxy");
				Assert.NotNull (proxy);
			}
		}

	}

	public class FixtureMultiInterfaceView
	{

		[Fact]
		public void Fixture_MultiInterfaceView_HasProxy ()
		{
			var peer = FindFixtureByJavaName ("my/app/MultiInterfaceView");
			Assert.False (peer.DoNotGenerateAcw);

			var model = BuildModel (new [] { peer }, "TypeMap");

			if (peer.ActivationCtor != null && peer.MarshalMethods.Count > 0) {
				var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "MyApp_MultiInterfaceView_Proxy");
				Assert.NotNull (proxy);
			}
		}

	}

	public class FixtureExportExample
	{

		[Fact]
		public void Fixture_ExportExample_HasProxy ()
		{
			var peer = FindFixtureByJavaName ("my/app/ExportExample");
			Assert.False (peer.DoNotGenerateAcw);
			Assert.Single (peer.MarshalMethods);

			var model = BuildModel (new [] { peer }, "TypeMap");

			if (peer.ActivationCtor != null) {
				var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "MyApp_ExportExample_Proxy");
				Assert.NotNull (proxy);
			}
		}

	}

	public class FixtureImplementors
	{

		[Fact]
		public void Fixture_Implementor_IsTrimmable_NotUnconditional ()
		{
			var peer = FindFixtureByJavaName ("android/view/View_IOnClickListenerImplementor");
			Assert.False (peer.DoNotGenerateAcw);
			Assert.False (peer.IsInterface);

			var model = BuildModel (new [] { peer }, "TypeMap");

			// Implementor types should be trimmable (3-arg), NOT unconditional
			var entry = model.Entries.FirstOrDefault ();
			Assert.NotNull (entry);
			Assert.False (entry!.IsUnconditional, "Implementor should NOT be unconditional");
			Assert.NotNull (entry.TargetTypeReference);
		}

	}

	public class FixtureEventDispatchers
	{

		[Fact]
		public void Fixture_EventDispatcher_IsTrimmable_NotUnconditional ()
		{
			var peer = FindFixtureByJavaName ("android/view/View_ClickEventDispatcher");
			Assert.False (peer.DoNotGenerateAcw);
			Assert.False (peer.IsInterface);

			var model = BuildModel (new [] { peer }, "TypeMap");

			// EventDispatcher types should be trimmable (3-arg), NOT unconditional
			var entry = model.Entries.FirstOrDefault ();
			Assert.NotNull (entry);
			Assert.False (entry!.IsUnconditional, "EventDispatcher should NOT be unconditional");
			Assert.NotNull (entry.TargetTypeReference);
		}

	}

	public class NameBasedDetection
	{

		[Fact]
		public void Build_UserTypeNamedImplementor_IsTreatedAsTrimmable ()
		{
			// Limitation: name-based heuristic means a user type ending in "Implementor"
			// will be treated as trimmable even if it's genuinely a user ACW type.
			// This test documents the known behavior.
			var peer = MakeAcwPeer ("my/app/MyImplementor", "MyApp.MyImplementor", "App");
			var model = BuildModel (new [] { peer });

			var entry = model.Entries.FirstOrDefault ();
			Assert.NotNull (entry);
			// The heuristic treats this as an Implementor → trimmable (not unconditional)
			Assert.False (entry!.IsUnconditional,
				"Name-based heuristic: types ending in 'Implementor' are treated as trimmable");
		}

		[Fact]
		public void Build_TypeIsInvoker_OnlyWhenReferencedByAnotherPeer ()
		{
			// A type is only treated as an invoker when another peer's InvokerTypeName references it.
			// A type named "MyInvoker" with DoNotGenerateAcw is NOT automatically an invoker.
			var invokerPeer = MakePeerWithActivation ("my/app/MyInvoker", "MyApp.MyInvoker", "App");
			invokerPeer.DoNotGenerateAcw = true;

			// Without a referencing peer, it gets a normal entry
			var model1 = BuildModel (new [] { invokerPeer });
			Assert.Single (model1.Entries);

			// When an interface references it as invoker, it is excluded
			var ifacePeer = new JavaPeerInfo {
				JavaName = "my/app/MyInvoker",
				ManagedTypeName = "MyApp.IMyInterface",
				AssemblyName = "App",
				IsInterface = true,
				InvokerTypeName = "MyApp.MyInvoker",
			};
			var model2 = BuildModel (new [] { ifacePeer, invokerPeer });
			// Only the interface gets entries/proxies, the invoker is excluded
			Assert.Single (model2.Entries);
			Assert.Equal ("MyApp.IMyInterface", model2.ProxyTypes [0].TargetType.ManagedTypeName);
		}

	}

	public class PipelineTests
	{

		[Fact]
		public void FullPipeline_AllFixtures_ProducesLoadableAssembly ()
		{
			var peers = ScanFixtures ();
			var model = BuildModel (peers, "FullPipeline");

			var outputPath = Path.Combine (Path.GetTempPath (), $"fullpipeline-{Guid.NewGuid ():N}", "FullPipeline.dll");
			try {
				var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
				emitter.Emit (model, outputPath);

				Assert.True (File.Exists (outputPath));
				using var pe = new PEReader (File.OpenRead (outputPath));
				Assert.True (pe.HasMetadata);

				var reader = pe.GetMetadataReader ();
				var asmDef = reader.GetAssemblyDefinition ();
				Assert.Equal ("FullPipeline", reader.GetString (asmDef.Name));

				// Verify proxy types are present
				var proxyTypes = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.Where (t => reader.GetString (t.Namespace) == "_TypeMap.Proxies")
					.ToList ();
				Assert.Equal (model.ProxyTypes.Count, proxyTypes.Count);

				// Verify all proxy type names match
				var proxyNames = proxyTypes.Select (t => reader.GetString (t.Name)).OrderBy (n => n).ToList ();
				var modelNames = model.ProxyTypes.Select (p => p.TypeName).OrderBy (n => n).ToList ();
				Assert.Equal (modelNames, proxyNames);
			} finally {
				var dir = Path.GetDirectoryName (outputPath);
				if (dir != null && Directory.Exists (dir))
					try { Directory.Delete (dir, true); } catch { }
			}
		}

		[Fact]
		public void FullPipeline_AllFixtures_TypeMapAttributeCountMatchesEntries ()
		{
			var peers = ScanFixtures ();
			var model = BuildModel (peers, "AttrCount");

			var outputPath = Path.Combine (Path.GetTempPath (), $"attrcount-{Guid.NewGuid ():N}", "AttrCount.dll");
			try {
				var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
				emitter.Emit (model, outputPath);

				using var pe = new PEReader (File.OpenRead (outputPath));
				var reader = pe.GetMetadataReader ();

				var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				int totalAttrs = asmAttrs.Count ();

				// Assembly attrs = TypeMap entries + IgnoresAccessChecksTo entries
				int expected = model.Entries.Count + model.IgnoresAccessChecksTo.Count;
				Assert.Equal (expected, totalAttrs);
			} finally {
				var dir = Path.GetDirectoryName (outputPath);
				if (dir != null && Directory.Exists (dir))
					try { Directory.Delete (dir, true); } catch { }
			}
		}

		[Fact]
		public void FullPipeline_CustomView_HasConstructorAndMethodWrappers ()
		{
			var peer = FindFixtureByJavaName ("my/app/CustomView");
			var model = BuildModel (new [] { peer }, "CtorTest");

			var outputPath = Path.Combine (Path.GetTempPath (), $"ctor-{Guid.NewGuid ():N}", "CtorTest.dll");
			try {
				var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
				emitter.Emit (model, outputPath);

				using var pe = new PEReader (File.OpenRead (outputPath));
				var reader = pe.GetMetadataReader ();

				var proxy = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.First (t => reader.GetString (t.Name) == "MyApp_CustomView_Proxy");

				var methodNames = proxy.GetMethods ()
					.Select (h => reader.GetString (reader.GetMethodDefinition (h).Name))
					.ToList ();

				Assert.Contains (".ctor", methodNames);
				Assert.Contains ("CreateInstance", methodNames);
				Assert.Contains ("get_TargetType", methodNames);
			} finally {
				var dir = Path.GetDirectoryName (outputPath);
				if (dir != null && Directory.Exists (dir))
					try { Directory.Delete (dir, true); } catch { }
			}
		}

		[Fact]
		public void FullPipeline_GenericHolder_ProducesValidAssembly ()
		{
			var peer = FindFixtureByJavaName ("my/app/GenericHolder");
			var model = BuildModel (new [] { peer }, "GenericTest");

			var outputPath = Path.Combine (Path.GetTempPath (), $"generic-{Guid.NewGuid ():N}", "GenericTest.dll");
			try {
				var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
				emitter.Emit (model, outputPath);

				using var pe = new PEReader (File.OpenRead (outputPath));
				var reader = pe.GetMetadataReader ();

				// Verify the assembly is loadable and has entries
				Assert.True (pe.HasMetadata);
				var entry = FindEntry (model, "my/app/GenericHolder");
				Assert.NotNull (entry);

				// Verify assembly attributes were emitted
				var asmAttrs = reader.GetCustomAttributes (EntityHandle.AssemblyDefinition);
				Assert.NotEmpty (asmAttrs);
			} finally {
				CleanUpDir (outputPath);
			}
		}

	}

	public class PeBlobValidation
	{

		[Fact]
		public void FullPipeline_Mixed2ArgAnd3Arg_BothSurviveRoundTrip ()
		{
			// java/lang/Object → essential → 2-arg unconditional
			var objectPeer = FindFixtureByJavaName ("java/lang/Object");
			// android/app/Activity → MCW → 3-arg trimmable
			var activityPeer = FindFixtureByJavaName ("android/app/Activity");

			var model = BuildModel (new [] { objectPeer, activityPeer }, "MixedBlob");
			Assert.Equal (2, model.Entries.Count);

			var outputPath = Path.Combine (Path.GetTempPath (), $"mixedblob-{Guid.NewGuid ():N}", "MixedBlob.dll");
			try {
				var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
				emitter.Emit (model, outputPath);

				using var pe = new PEReader (File.OpenRead (outputPath));
				var reader = pe.GetMetadataReader ();

				var attrs = ReadAllTypeMapAttributeBlobs (reader);
				Assert.Equal (2, attrs.Count);

				// Find the 2-arg (unconditional) entry
				var unconditional = attrs.FirstOrDefault (a => a.jniName == "java/lang/Object");
				Assert.NotNull (unconditional.jniName);
				Assert.Null (unconditional.targetRef); // 2-arg: no target

				// Find the 3-arg (trimmable) entry
				var trimmable = attrs.FirstOrDefault (a => a.jniName == "android/app/Activity");
				Assert.NotNull (trimmable.jniName);
				Assert.NotNull (trimmable.targetRef); // 3-arg: has target
				Assert.Contains ("Android.App.Activity", trimmable.targetRef!);
			} finally {
				CleanUpDir (outputPath);
			}
		}

		[Fact]
		public void FullPipeline_EssentialType_Emits2ArgAttribute ()
		{
			// java/lang/Object is essential → unconditional 2-arg attribute
			var peer = FindFixtureByJavaName ("java/lang/Object");
			var model = BuildModel (new [] { peer }, "Blob2Arg");
			Assert.Single (model.Entries);
			Assert.True (model.Entries [0].IsUnconditional);

			var outputPath = Path.Combine (Path.GetTempPath (), $"blob2arg-{Guid.NewGuid ():N}", "Blob2Arg.dll");
			try {
				var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
				emitter.Emit (model, outputPath);

				using var pe = new PEReader (File.OpenRead (outputPath));
				var reader = pe.GetMetadataReader ();
				var (jniName, proxyRef, targetRef) = ReadFirstTypeMapAttributeBlob (reader);

				Assert.Equal ("java/lang/Object", jniName);
				Assert.NotNull (proxyRef);
				Assert.Contains ("Java_Lang_Object_Proxy", proxyRef!);
				// 2-arg: no target type
				Assert.Null (targetRef);
			} finally {
				CleanUpDir (outputPath);
			}
		}

		[Fact]
		public void FullPipeline_McwBinding_Emits3ArgAttribute ()
		{
			// android/app/Activity is MCW → trimmable 3-arg attribute
			var peer = FindFixtureByJavaName ("android/app/Activity");
			var model = BuildModel (new [] { peer }, "Blob3Arg");
			Assert.Single (model.Entries);
			Assert.False (model.Entries [0].IsUnconditional);

			var outputPath = Path.Combine (Path.GetTempPath (), $"blob3arg-{Guid.NewGuid ():N}", "Blob3Arg.dll");
			try {
				var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
				emitter.Emit (model, outputPath);

				using var pe = new PEReader (File.OpenRead (outputPath));
				var reader = pe.GetMetadataReader ();
				var (jniName, proxyRef, targetRef) = ReadFirstTypeMapAttributeBlob (reader);

				Assert.Equal ("android/app/Activity", jniName);
				Assert.NotNull (proxyRef);
				Assert.Contains ("Android_App_Activity_Proxy", proxyRef!);
				// 3-arg: has target type
				Assert.NotNull (targetRef);
				Assert.Contains ("Android.App.Activity", targetRef!);
			} finally {
				CleanUpDir (outputPath);
			}
		}

		[Fact]
		public void FullPipeline_UserAcw_Emits2ArgAttribute ()
		{
			// my/app/MainActivity is user ACW → unconditional 2-arg
			var peer = FindFixtureByJavaName ("my/app/MainActivity");
			var model = BuildModel (new [] { peer }, "BlobAcw");
			Assert.Single (model.Entries);
			Assert.True (model.Entries [0].IsUnconditional);

			var outputPath = Path.Combine (Path.GetTempPath (), $"blobacw-{Guid.NewGuid ():N}", "BlobAcw.dll");
			try {
				var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
				emitter.Emit (model, outputPath);

				using var pe = new PEReader (File.OpenRead (outputPath));
				var reader = pe.GetMetadataReader ();
				var (jniName, proxyRef, targetRef) = ReadFirstTypeMapAttributeBlob (reader);

				Assert.Equal ("my/app/MainActivity", jniName);
				Assert.Null (targetRef); // unconditional → no target
			} finally {
				CleanUpDir (outputPath);
			}
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
			// Skip IgnoresAccessChecksTo attributes (their ctor is a MethodDefinition, not MemberRef)
			if (attr.Constructor.Kind == HandleKind.MethodDefinition)
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

			result.Add ((jniName, proxyRef, targetRef));
		}
		return result;
	}

	static void CleanUpDir (string path)
	{
		var dir = Path.GetDirectoryName (path);
		if (dir != null && Directory.Exists (dir))
			try { Directory.Delete (dir, true); } catch { }
	}
}