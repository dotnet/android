using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Xunit;

namespace Microsoft.Android.Build.TypeMap.Tests;

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

	List<JavaPeerInfo> ScanFixtures ()
	{
		using var scanner = new JavaPeerScanner ();
		return scanner.Scan (new [] { TestFixtureAssemblyPath });
	}

	TypeMapAssemblyData BuildModel (IReadOnlyList<JavaPeerInfo> peers, string? assemblyName = null)
	{
		var outputPath = Path.Combine ("/tmp", (assemblyName ?? "TestTypeMap") + ".dll");
		var builder = new ModelBuilder ();
		return builder.Build (peers, outputPath, assemblyName);
	}

	// ---- Basic model structure ----

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
		var builder = new ModelBuilder ();
		var model = builder.Build (Array.Empty<JavaPeerInfo> (), "/some/path/Foo.Bar.dll");
		Assert.Equal ("Foo.Bar", model.AssemblyName);
		Assert.Equal ("Foo.Bar.dll", model.ModuleName);
	}

	[Fact]
	public void Build_ExplicitAssemblyName_OverridesOutputPath ()
	{
		var builder = new ModelBuilder ();
		var model = builder.Build (Array.Empty<JavaPeerInfo> (), "/some/path/Foo.dll", "MyAssembly");
		Assert.Equal ("MyAssembly", model.AssemblyName);
	}

	[Fact]
	public void Build_DefaultIgnoresAccessChecksTo ()
	{
		var model = BuildModel (Array.Empty<JavaPeerInfo> ());
		Assert.Contains ("Mono.Android", model.IgnoresAccessChecksTo);
		Assert.Contains ("Java.Interop", model.IgnoresAccessChecksTo);
	}

	// ---- TypeMap entries ----

	[Fact]
	public void Build_CreatesOneEntryPerPeer ()
	{
		var peers = new List<JavaPeerInfo> {
			MakeMcwPeer ("java/lang/Object", "Java.Lang.Object", "Mono.Android"),
			MakeMcwPeer ("android/app/Activity", "Android.App.Activity", "Mono.Android"),
		};

		var model = BuildModel (peers);
		Assert.Equal (2, model.Entries.Count);
		Assert.Equal ("java/lang/Object", model.Entries [0].JniName);
		Assert.Equal ("android/app/Activity", model.Entries [1].JniName);
	}

	[Fact]
	public void Build_DuplicateJniNames_KeepsFirstOnly ()
	{
		var peers = new List<JavaPeerInfo> {
			MakeMcwPeer ("test/Dup", "Test.First", "A"),
			MakeMcwPeer ("test/Dup", "Test.Second", "A"),
		};

		var model = BuildModel (peers);
		Assert.Single (model.Entries);
		Assert.Equal ("test/Dup", model.Entries [0].JniName);
		// First one wins - type reference should point to Test.First
		Assert.Contains ("Test.First", model.Entries [0].TypeReference);
	}

	[Fact]
	public void Build_McwPeerWithoutActivation_NoProxy ()
	{
		var peer = MakeMcwPeer ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
		// No activation ctor, no invoker → no proxy
		var model = BuildModel (new [] { peer });

		Assert.Empty (model.ProxyTypes);
		Assert.Single (model.Entries);
		Assert.Contains ("Java.Lang.Object, Mono.Android", model.Entries [0].TypeReference);
	}

	// ---- Proxy types ----

	[Fact]
	public void Build_PeerWithActivationCtor_CreatesProxy ()
	{
		var peer = MakePeerWithActivation ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
		var model = BuildModel (new [] { peer }, "MyTypeMap");

		Assert.Single (model.ProxyTypes);
		var proxy = model.ProxyTypes [0];
		Assert.Equal ("java_lang_Object_Proxy", proxy.TypeName);
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
	public void Build_ProxyNaming_ReplacesSlashAndDollar ()
	{
		var peer = MakePeerWithActivation ("com/example/Outer$Inner", "Com.Example.Outer.Inner", "App");
		var model = BuildModel (new [] { peer });

		Assert.Single (model.ProxyTypes);
		Assert.Equal ("com_example_Outer_Inner_Proxy", model.ProxyTypes [0].TypeName);
	}

	[Fact]
	public void Build_EntryPointsToProxy_WhenProxyExists ()
	{
		var peer = MakePeerWithActivation ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
		var model = BuildModel (new [] { peer }, "MyTypeMap");

		var entry = model.Entries [0];
		Assert.Contains ("java_lang_Object_Proxy", entry.TypeReference);
		Assert.Contains ("MyTypeMap", entry.TypeReference);
	}

	// ---- ACW detection ----

	[Fact]
	public void Build_AcwType_IsAcwTrue ()
	{
		var peer = MakeAcwPeer ("my/app/Main", "MyApp.MainActivity", "App");
		var model = BuildModel (new [] { peer });

		Assert.Single (model.ProxyTypes);
		Assert.True (model.ProxyTypes [0].IsAcw);
		Assert.True (model.ProxyTypes [0].ImplementsIAndroidCallableWrapper);
	}

	[Fact]
	public void Build_McwType_IsAcwFalse ()
	{
		var peer = MakePeerWithActivation ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
		var model = BuildModel (new [] { peer });

		Assert.Single (model.ProxyTypes);
		Assert.False (model.ProxyTypes [0].IsAcw);
		Assert.False (model.ProxyTypes [0].ImplementsIAndroidCallableWrapper);
	}

	[Fact]
	public void Build_InterfaceWithMarshalMethods_IsNotAcw ()
	{
		var peer = new JavaPeerInfo {
			JavaName = "android/view/View$OnClickListener",
			ManagedTypeName = "Android.Views.View+IOnClickListener",
			ManagedTypeNamespace = "Android.Views",
			ManagedTypeShortName = "IOnClickListener",
			AssemblyName = "Mono.Android",
			IsInterface = true,
			InvokerTypeName = "Android.Views.View+IOnClickListenerInvoker",
			MarshalMethods = new List<MarshalMethodInfo> {
				MakeMarshalMethod ("onClick", "n_OnClick", "(Landroid/view/View;)V"),
			},
		};

		var model = BuildModel (new [] { peer });
		Assert.Single (model.ProxyTypes);
		// Interface is NOT an ACW even with marshal methods
		Assert.False (model.ProxyTypes [0].IsAcw);
	}

	[Fact]
	public void Build_DoNotGenerateAcw_IsNotAcw ()
	{
		var peer = MakePeerWithActivation ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
		peer.DoNotGenerateAcw = true;
		peer.MarshalMethods = new List<MarshalMethodInfo> {
			MakeMarshalMethod ("toString", "n_ToString", "()Ljava/lang/String;"),
		};

		var model = BuildModel (new [] { peer });
		Assert.Single (model.ProxyTypes);
		Assert.False (model.ProxyTypes [0].IsAcw);
	}

	// ---- UCO methods ----

	[Fact]
	public void Build_AcwWithMarshalMethods_CreatesUcoMethods ()
	{
		var peer = MakeAcwPeer ("my/app/Main", "MyApp.MainActivity", "App");
		peer.MarshalMethods = new List<MarshalMethodInfo> {
			MakeMarshalMethod ("<init>", "n_ctor", "()V", isConstructor: true),
			MakeMarshalMethod ("onCreate", "n_OnCreate", "(Landroid/os/Bundle;)V"),
			MakeMarshalMethod ("onResume", "n_OnResume", "()V"),
		};

		var model = BuildModel (new [] { peer });
		var proxy = model.ProxyTypes [0];

		Assert.Equal (2, proxy.UcoMethods.Count);
		Assert.Equal ("n_onCreate_uco_0", proxy.UcoMethods [0].WrapperName);
		Assert.Equal ("n_OnCreate", proxy.UcoMethods [0].CallbackMethodName);
		Assert.Equal ("(Landroid/os/Bundle;)V", proxy.UcoMethods [0].JniSignature);

		Assert.Equal ("n_onResume_uco_1", proxy.UcoMethods [1].WrapperName);
		Assert.Equal ("n_OnResume", proxy.UcoMethods [1].CallbackMethodName);
	}

	[Fact]
	public void Build_UcoMethod_CallbackTypeIsDeclaringType ()
	{
		var mm = MakeMarshalMethod ("toString", "n_ToString", "()Ljava/lang/String;");
		mm.DeclaringTypeName = "Java.Lang.Object";
		mm.DeclaringAssemblyName = "Mono.Android";

		var peer = MakeAcwPeer ("my/app/Main", "MyApp.MainActivity", "App");
		peer.MarshalMethods = new List<MarshalMethodInfo> {
			MakeMarshalMethod ("<init>", "n_ctor", "()V", isConstructor: true),
			mm,
		};

		var model = BuildModel (new [] { peer });
		var uco = model.ProxyTypes [0].UcoMethods [0];
		Assert.Equal ("Java.Lang.Object", uco.CallbackType.ManagedTypeName);
		Assert.Equal ("Mono.Android", uco.CallbackType.AssemblyName);
	}

	[Fact]
	public void Build_UcoMethod_FallsBackToPeerType_WhenDeclaringTypeEmpty ()
	{
		var peer = MakeAcwPeer ("my/app/Main", "MyApp.MainActivity", "App");
		peer.MarshalMethods = new List<MarshalMethodInfo> {
			MakeMarshalMethod ("<init>", "n_ctor", "()V", isConstructor: true),
			MakeMarshalMethod ("onPause", "n_OnPause", "()V"),
		};

		var model = BuildModel (new [] { peer });
		var uco = model.ProxyTypes [0].UcoMethods [0];
		Assert.Equal ("MyApp.MainActivity", uco.CallbackType.ManagedTypeName);
		Assert.Equal ("App", uco.CallbackType.AssemblyName);
	}

	[Fact]
	public void Build_ConstructorsInMarshalMethods_SkippedFromUcoMethods ()
	{
		var peer = MakeAcwPeer ("my/app/Main", "MyApp.MainActivity", "App");
		peer.MarshalMethods = new List<MarshalMethodInfo> {
			MakeMarshalMethod ("<init>", "n_ctor", "()V", isConstructor: true),
			MakeMarshalMethod ("<init>", "n_ctor2", "()V", isConstructor: true),
			MakeMarshalMethod ("onStart", "n_OnStart", "()V"),
		};

		var model = BuildModel (new [] { peer });
		var proxy = model.ProxyTypes [0];

		// Only 1 UCO method (constructors are skipped from UcoMethods)
		Assert.Single (proxy.UcoMethods);
		Assert.Equal ("n_onStart_uco_0", proxy.UcoMethods [0].WrapperName);
	}

	// ---- UCO constructors ----

	[Fact]
	public void Build_AcwWithConstructors_CreatesUcoConstructors ()
	{
		var peer = MakeAcwPeer ("my/app/Main", "MyApp.MainActivity", "App");

		var model = BuildModel (new [] { peer });
		var proxy = model.ProxyTypes [0];

		Assert.Single (proxy.UcoConstructors);
		Assert.Equal ("nctor_0_uco", proxy.UcoConstructors [0].WrapperName);
		Assert.Equal ("MyApp.MainActivity", proxy.UcoConstructors [0].TargetType.ManagedTypeName);
	}

	[Fact]
	public void Build_PeerWithoutActivationCtor_NoUcoConstructors ()
	{
		// Peer with marshal methods but no activation ctor
		var peer = new JavaPeerInfo {
			JavaName = "my/app/Foo",
			ManagedTypeName = "MyApp.Foo",
			ManagedTypeNamespace = "MyApp",
			ManagedTypeShortName = "Foo",
			AssemblyName = "App",
			InvokerTypeName = "MyApp.FooInvoker", // has invoker → will create proxy
			MarshalMethods = new List<MarshalMethodInfo> {
				MakeMarshalMethod ("bar", "n_Bar", "()V"),
			},
			JavaConstructors = new List<JavaConstructorInfo> {
				new JavaConstructorInfo { ConstructorIndex = 0, JniSignature = "()V" },
			},
		};

		var model = BuildModel (new [] { peer });
		var proxy = model.ProxyTypes [0];

		Assert.Empty (proxy.UcoConstructors);
	}

	// ---- Native registrations ----

	[Fact]
	public void Build_NativeRegistrations_MatchUcoMethods ()
	{
		var peer = MakeAcwPeer ("my/app/Main", "MyApp.MainActivity", "App");
		peer.MarshalMethods = new List<MarshalMethodInfo> {
			MakeMarshalMethod ("<init>", "n_ctor", "()V", isConstructor: true),
			MakeMarshalMethod ("onCreate", "n_OnCreate", "(Landroid/os/Bundle;)V"),
		};

		var model = BuildModel (new [] { peer });
		var proxy = model.ProxyTypes [0];

		// 1 registration for method + 1 for constructor
		Assert.Equal (2, proxy.NativeRegistrations.Count);

		var methodReg = proxy.NativeRegistrations [0];
		Assert.Equal ("n_OnCreate", methodReg.JniMethodName);
		Assert.Equal ("(Landroid/os/Bundle;)V", methodReg.JniSignature);
		Assert.Equal ("n_onCreate_uco_0", methodReg.WrapperMethodName);

		var ctorReg = proxy.NativeRegistrations [1];
		Assert.Equal ("nctor_0", ctorReg.JniMethodName);
		Assert.Equal ("nctor_0_uco", ctorReg.WrapperMethodName);
	}

	[Fact]
	public void Build_NonAcwProxy_NoNativeRegistrations ()
	{
		var peer = MakePeerWithActivation ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
		var model = BuildModel (new [] { peer });

		Assert.Single (model.ProxyTypes);
		Assert.Empty (model.ProxyTypes [0].NativeRegistrations);
	}

	// ---- Full fixture scan ----

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
		Assert.All (model.Entries, e => Assert.False (string.IsNullOrEmpty (e.TypeReference)));
	}

	[Fact]
	public void Build_FromScannedFixtures_NoProxiesForMcwWithoutActivation ()
	{
		var peers = ScanFixtures ();
		var model = BuildModel (peers);

		// Proxy type names should all end with _Proxy
		Assert.All (model.ProxyTypes, p => Assert.EndsWith ("_Proxy", p.TypeName));
	}

	[Fact]
	public void Build_FromScannedFixtures_AcwTypesHaveUcoMethods ()
	{
		var peers = ScanFixtures ();
		var model = BuildModel (peers);

		var acwProxies = model.ProxyTypes.Where (p => p.IsAcw).ToList ();
		Assert.NotEmpty (acwProxies);

		// ACW proxies should have registrations
		foreach (var proxy in acwProxies) {
			Assert.NotEmpty (proxy.NativeRegistrations);
		}
	}

	// ---- Helpers ----

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

	// ========================================================================
	// Fixture-based tests: scan the real TestFixtures.dll and verify model output
	// ========================================================================

	JavaPeerInfo FindFixtureByJavaName (string javaName)
	{
		var peers = ScanFixtures ();
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
	}

	JavaPeerProxyData? FindProxy (TypeMapAssemblyData model, string proxyTypeName)
	{
		return model.ProxyTypes.FirstOrDefault (p => p.TypeName == proxyTypeName);
	}

	TypeMapAttributeData? FindEntry (TypeMapAssemblyData model, string jniName)
	{
		return model.Entries.FirstOrDefault (e => e.JniName == jniName);
	}

	// ---- MCW types from fixtures ----

	[Fact]
	public void Fixture_JavaLangObject_HasActivation_CreatesProxy ()
	{
		var peer = FindFixtureByJavaName ("java/lang/Object");
		var model = BuildModel (new [] { peer }, "TypeMap");

		var proxy = FindProxy (model, "java_lang_Object_Proxy");
		Assert.NotNull (proxy);
		Assert.True (proxy!.HasActivation);
		Assert.Equal ("Java.Lang.Object", proxy.TargetType.ManagedTypeName);
		Assert.Equal ("TestFixtures", proxy.TargetType.AssemblyName);
		// MCW with DoNotGenerateAcw → not ACW
		Assert.False (proxy.IsAcw);
		Assert.Empty (proxy.UcoMethods);
		Assert.Empty (proxy.UcoConstructors);
		Assert.Empty (proxy.NativeRegistrations);
	}

	[Fact]
	public void Fixture_Activity_HasActivation_CreatesProxy ()
	{
		var peer = FindFixtureByJavaName ("android/app/Activity");
		var model = BuildModel (new [] { peer }, "TypeMap");

		var proxy = FindProxy (model, "android_app_Activity_Proxy");
		Assert.NotNull (proxy);
		Assert.True (proxy!.HasActivation);
		Assert.Equal ("Android.App.Activity", proxy.TargetType.ManagedTypeName);
		// MCW: DoNotGenerateAcw=true → not ACW (even though it has marshal methods)
		Assert.False (proxy.IsAcw);
	}

	[Fact]
	public void Fixture_Activity_Entry_PointsToProxy ()
	{
		var peer = FindFixtureByJavaName ("android/app/Activity");
		var model = BuildModel (new [] { peer }, "MyTypeMap");

		var entry = FindEntry (model, "android/app/Activity");
		Assert.NotNull (entry);
		Assert.Contains ("android_app_Activity_Proxy", entry!.TypeReference);
		Assert.Contains ("MyTypeMap", entry.TypeReference);
	}

	[Fact]
	public void Fixture_Throwable_HasActivation ()
	{
		var peer = FindFixtureByJavaName ("java/lang/Throwable");
		var model = BuildModel (new [] { peer }, "TypeMap");

		var proxy = FindProxy (model, "java_lang_Throwable_Proxy");
		Assert.NotNull (proxy);
		Assert.True (proxy!.HasActivation);
		Assert.False (proxy.IsAcw);
	}

	[Fact]
	public void Fixture_Exception_HasActivation ()
	{
		var peer = FindFixtureByJavaName ("java/lang/Exception");
		var model = BuildModel (new [] { peer }, "TypeMap");

		var proxy = FindProxy (model, "java_lang_Exception_Proxy");
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
			var proxy = FindProxy (model, "android_content_Context_Proxy");
			Assert.NotNull (proxy);
			Assert.False (proxy!.IsAcw);
		}
	}

	[Fact]
	public void Fixture_View_HasActivation ()
	{
		var peer = FindFixtureByJavaName ("android/view/View");
		var model = BuildModel (new [] { peer }, "TypeMap");

		if (peer.ActivationCtor != null) {
			var proxy = FindProxy (model, "android_view_View_Proxy");
			Assert.NotNull (proxy);
		}
	}

	[Fact]
	public void Fixture_Button_HasActivation ()
	{
		var peer = FindFixtureByJavaName ("android/widget/Button");
		var model = BuildModel (new [] { peer }, "TypeMap");

		if (peer.ActivationCtor != null) {
			var proxy = FindProxy (model, "android_widget_Button_Proxy");
			Assert.NotNull (proxy);
		}
	}

	// ---- User ACW types from fixtures ----

	[Fact]
	public void Fixture_MainActivity_IsAcw ()
	{
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		Assert.False (peer.DoNotGenerateAcw);
		Assert.NotEmpty (peer.MarshalMethods);
		Assert.NotNull (peer.ActivationCtor);

		var model = BuildModel (new [] { peer }, "TypeMap");
		var proxy = FindProxy (model, "my_app_MainActivity_Proxy");
		Assert.NotNull (proxy);
		Assert.True (proxy!.IsAcw);
		Assert.True (proxy.ImplementsIAndroidCallableWrapper);
		Assert.True (proxy.HasActivation);
	}

	[Fact]
	public void Fixture_MainActivity_UcoMethods ()
	{
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		var model = BuildModel (new [] { peer }, "TypeMap");
		var proxy = FindProxy (model, "my_app_MainActivity_Proxy")!;

		// Should have UCO wrappers for non-constructor marshal methods
		var nonCtorMethods = peer.MarshalMethods.Where (m => !m.IsConstructor).ToList ();
		Assert.Equal (nonCtorMethods.Count, proxy.UcoMethods.Count);

		// Verify the onCreate wrapper
		var onCreateUco = proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnCreate");
		Assert.NotNull (onCreateUco);
		Assert.Equal ("(Landroid/os/Bundle;)V", onCreateUco!.JniSignature);
		Assert.StartsWith ("n_onCreate_uco_", onCreateUco.WrapperName);
	}

	[Fact]
	public void Fixture_MainActivity_NativeRegistrations ()
	{
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		var model = BuildModel (new [] { peer }, "TypeMap");
		var proxy = FindProxy (model, "my_app_MainActivity_Proxy")!;

		Assert.NotEmpty (proxy.NativeRegistrations);

		// Should register n_OnCreate
		var onCreateReg = proxy.NativeRegistrations.FirstOrDefault (r => r.JniMethodName == "n_OnCreate");
		Assert.NotNull (onCreateReg);
		Assert.Equal ("(Landroid/os/Bundle;)V", onCreateReg!.JniSignature);
	}

	[Fact]
	public void Fixture_MyHelper_IsAcw ()
	{
		var peer = FindFixtureByJavaName ("my/app/MyHelper");
		Assert.False (peer.DoNotGenerateAcw);

		var model = BuildModel (new [] { peer }, "TypeMap");

		// MyHelper has marshal methods and is not DoNotGenerateAcw
		// Whether it's ACW depends on: not interface, has marshal methods, not DoNotGenerateAcw
		if (peer.MarshalMethods.Count > 0 && peer.ActivationCtor != null) {
			var proxy = FindProxy (model, "my_app_MyHelper_Proxy");
			Assert.NotNull (proxy);
		}
	}

	// ---- TouchHandler: various JNI types ----

	[Fact]
	public void Fixture_TouchHandler_AllUcoMethods ()
	{
		var peer = FindFixtureByJavaName ("my/app/TouchHandler");
		var model = BuildModel (new [] { peer }, "TypeMap");
		var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "my_app_TouchHandler_Proxy");
		Assert.NotNull (proxy);

		var nonCtorMethods = peer.MarshalMethods.Where (m => !m.IsConstructor).ToList ();
		Assert.Equal (nonCtorMethods.Count, proxy!.UcoMethods.Count);

		// onTouch: (Landroid/view/View;I)Z
		var onTouchUco = proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnTouch");
		Assert.NotNull (onTouchUco);
		Assert.Equal ("(Landroid/view/View;I)Z", onTouchUco!.JniSignature);

		// onFocusChange: (Landroid/view/View;Z)V
		var onFocusUco = proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnFocusChange");
		Assert.NotNull (onFocusUco);
		Assert.Equal ("(Landroid/view/View;Z)V", onFocusUco!.JniSignature);

		// onScroll: (IFJD)V
		var onScrollUco = proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnScroll");
		Assert.NotNull (onScrollUco);
		Assert.Equal ("(IFJD)V", onScrollUco!.JniSignature);

		// getText: ()Ljava/lang/String;
		var getTextUco = proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_GetText");
		Assert.NotNull (getTextUco);
		Assert.Equal ("()Ljava/lang/String;", getTextUco!.JniSignature);

		// setItems: ([Ljava/lang/String;)V
		var setItemsUco = proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_SetItems");
		Assert.NotNull (setItemsUco);
		Assert.Equal ("([Ljava/lang/String;)V", setItemsUco!.JniSignature);
	}

	[Fact]
	public void Fixture_TouchHandler_NativeRegistrationsMatchUcoMethods ()
	{
		var peer = FindFixtureByJavaName ("my/app/TouchHandler");
		var model = BuildModel (new [] { peer }, "TypeMap");
		var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "my_app_TouchHandler_Proxy")!;

		// Every UCO method should have a matching registration
		foreach (var uco in proxy.UcoMethods) {
			var reg = proxy.NativeRegistrations.FirstOrDefault (r => r.WrapperMethodName == uco.WrapperName);
			Assert.NotNull (reg);
			Assert.Equal (uco.JniSignature, reg!.JniSignature);
		}
	}

	// ---- CustomView: registered constructors ----

	[Fact]
	public void Fixture_CustomView_HasTwoConstructorWrappers ()
	{
		var peer = FindFixtureByJavaName ("my/app/CustomView");
		Assert.Equal (2, peer.JavaConstructors.Count);

		var model = BuildModel (new [] { peer }, "TypeMap");
		var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "my_app_CustomView_Proxy");
		Assert.NotNull (proxy);

		if (proxy!.IsAcw) {
			Assert.Equal (2, proxy.UcoConstructors.Count);
			Assert.Equal ("nctor_0_uco", proxy.UcoConstructors [0].WrapperName);
			Assert.Equal ("nctor_1_uco", proxy.UcoConstructors [1].WrapperName);
			Assert.Equal ("MyApp.CustomView", proxy.UcoConstructors [0].TargetType.ManagedTypeName);
			Assert.Equal ("MyApp.CustomView", proxy.UcoConstructors [1].TargetType.ManagedTypeName);

			// Constructor registrations
			var ctorRegs = proxy.NativeRegistrations.Where (r => r.JniMethodName.StartsWith ("nctor_")).ToList ();
			Assert.Equal (2, ctorRegs.Count);
		}
	}

	// ---- Interface types ----

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

	[Fact]
	public void Fixture_IOnClickListener_IsNotAcw ()
	{
		var peers = ScanFixtures ();
		var listener = peers.FirstOrDefault (p => p.ManagedTypeName == "Android.Views.IOnClickListener");
		Assert.NotNull (listener);

		var model = BuildModel (new [] { listener! }, "TypeMap");

		// Interface → not ACW even though it has marshal methods
		foreach (var proxy in model.ProxyTypes) {
			Assert.False (proxy.IsAcw);
		}
	}

	// ---- Nested types ----

	[Fact]
	public void Fixture_OuterInner_ProxyNaming ()
	{
		var peer = FindFixtureByJavaName ("my/app/Outer$Inner");
		var model = BuildModel (new [] { peer }, "TypeMap");

		// $ gets replaced with _
		var entry = FindEntry (model, "my/app/Outer$Inner");
		Assert.NotNull (entry);

		if (peer.ActivationCtor != null) {
			var proxy = FindProxy (model, "my_app_Outer_Inner_Proxy");
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
			var proxy = FindProxy (model, "my_app_ICallback_Result_Proxy");
			Assert.NotNull (proxy);
			Assert.Equal ("MyApp.ICallback+Result", proxy!.TargetType.ManagedTypeName);
		}
	}

	// ---- Duplicate JNI names across interface + invoker ----

	[Fact]
	public void Fixture_InterfaceAndInvoker_ShareJniName_OnlyFirst ()
	{
		var peers = ScanFixtures ();
		// IOnClickListener and IOnClickListenerInvoker share "android/view/View$OnClickListener"
		var clickPeers = peers.Where (p => p.JavaName == "android/view/View$OnClickListener").ToList ();
		Assert.Equal (2, clickPeers.Count);

		var model = BuildModel (clickPeers, "TypeMap");

		// Dedup: only one entry for this JNI name
		var entries = model.Entries.Where (e => e.JniName == "android/view/View$OnClickListener").ToList ();
		Assert.Single (entries);
	}

	// ---- GenericHolder ----

	[Fact]
	public void Fixture_GenericHolder_Entry ()
	{
		var peer = FindFixtureByJavaName ("my/app/GenericHolder");
		Assert.True (peer.IsGenericDefinition);

		var model = BuildModel (new [] { peer }, "TypeMap");
		var entry = FindEntry (model, "my/app/GenericHolder");
		Assert.NotNull (entry);
	}

	// ---- AbstractBase ----

	[Fact]
	public void Fixture_AbstractBase_IsAcw ()
	{
		var peer = FindFixtureByJavaName ("my/app/AbstractBase");
		Assert.True (peer.IsAbstract);
		Assert.False (peer.DoNotGenerateAcw);

		var model = BuildModel (new [] { peer }, "TypeMap");

		// AbstractBase has marshal methods (doWork) and activation ctor
		if (peer.ActivationCtor != null && peer.MarshalMethods.Count > 0) {
			var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "my_app_AbstractBase_Proxy");
			Assert.NotNull (proxy);
			Assert.True (proxy!.IsAcw);
		}
	}

	// ---- ClickableView: implements interface ----

	[Fact]
	public void Fixture_ClickableView_IsAcw ()
	{
		var peer = FindFixtureByJavaName ("my/app/ClickableView");
		Assert.False (peer.DoNotGenerateAcw);

		var model = BuildModel (new [] { peer }, "TypeMap");

		if (peer.ActivationCtor != null && peer.MarshalMethods.Count > 0) {
			var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "my_app_ClickableView_Proxy");
			Assert.NotNull (proxy);
			Assert.True (proxy!.IsAcw);
			// Should have onClick UCO wrapper
			var onClick = proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnClick");
			Assert.NotNull (onClick);
			Assert.Equal ("(Landroid/view/View;)V", onClick!.JniSignature);
		}
	}

	// ---- MultiInterfaceView ----

	[Fact]
	public void Fixture_MultiInterfaceView_HasAllUcoMethods ()
	{
		var peer = FindFixtureByJavaName ("my/app/MultiInterfaceView");
		Assert.False (peer.DoNotGenerateAcw);

		var model = BuildModel (new [] { peer }, "TypeMap");

		if (peer.ActivationCtor != null && peer.MarshalMethods.Count > 0) {
			var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "my_app_MultiInterfaceView_Proxy");
			Assert.NotNull (proxy);

			// Should have onClick and onLongClick UCO wrappers
			Assert.NotNull (proxy!.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnClick"));
			Assert.NotNull (proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnLongClick"));
		}
	}

	// ---- ExportExample ----

	[Fact]
	public void Fixture_ExportExample_IsAcw ()
	{
		var peer = FindFixtureByJavaName ("my/app/ExportExample");
		Assert.False (peer.DoNotGenerateAcw);
		Assert.Single (peer.MarshalMethods);

		var model = BuildModel (new [] { peer }, "TypeMap");

		if (peer.ActivationCtor != null) {
			var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "my_app_ExportExample_Proxy");
			Assert.NotNull (proxy);
		}
	}

	// ---- Full pipeline: scan → model → emit → read back ----

	[Fact]
	public void FullPipeline_AllFixtures_ProducesLoadableAssembly ()
	{
		var peers = ScanFixtures ();
		var model = BuildModel (peers, "FullPipeline");

		var outputPath = Path.Combine (Path.GetTempPath (), $"fullpipeline-{Guid.NewGuid ():N}", "FullPipeline.dll");
		try {
			var emitter = new TypeMapAssemblyEmitter ();
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
			var emitter = new TypeMapAssemblyEmitter ();
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
	public void FullPipeline_TouchHandler_AcwProxyHasUcoAttributes ()
	{
		var peer = FindFixtureByJavaName ("my/app/TouchHandler");
		var model = BuildModel (new [] { peer }, "UcoAttrTest");

		var outputPath = Path.Combine (Path.GetTempPath (), $"ucoattr-{Guid.NewGuid ():N}", "UcoAttrTest.dll");
		try {
			var emitter = new TypeMapAssemblyEmitter ();
			emitter.Emit (model, outputPath);

			using var pe = new PEReader (File.OpenRead (outputPath));
			var reader = pe.GetMetadataReader ();

			var proxy = reader.TypeDefinitions
				.Select (h => reader.GetTypeDefinition (h))
				.First (t => reader.GetString (t.Name) == "my_app_TouchHandler_Proxy");

			var methods = proxy.GetMethods ()
				.Select (h => reader.GetMethodDefinition (h))
				.ToList ();

			var ucoMethods = methods.Where (m => reader.GetString (m.Name).Contains ("_uco_")).ToList ();
			Assert.NotEmpty (ucoMethods);

			// Each UCO method should have [UnmanagedCallersOnly]
			foreach (var uco in ucoMethods) {
				var attrs = uco.GetCustomAttributes ().Select (h => reader.GetCustomAttribute (h)).ToList ();
				Assert.NotEmpty (attrs);
			}
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
			var emitter = new TypeMapAssemblyEmitter ();
			emitter.Emit (model, outputPath);

			using var pe = new PEReader (File.OpenRead (outputPath));
			var reader = pe.GetMetadataReader ();

			var proxy = reader.TypeDefinitions
				.Select (h => reader.GetTypeDefinition (h))
				.First (t => reader.GetString (t.Name) == "my_app_CustomView_Proxy");

			var methodNames = proxy.GetMethods ()
				.Select (h => reader.GetString (reader.GetMethodDefinition (h).Name))
				.ToList ();

			Assert.Contains (".ctor", methodNames);
			Assert.Contains ("CreateInstance", methodNames);
			Assert.Contains ("get_TargetType", methodNames);

			if (model.ProxyTypes [0].IsAcw) {
				Assert.Contains ("RegisterNatives", methodNames);
				Assert.Contains (methodNames, m => m.StartsWith ("nctor_") && m.EndsWith ("_uco"));
			}
		} finally {
			var dir = Path.GetDirectoryName (outputPath);
			if (dir != null && Directory.Exists (dir))
				try { Directory.Delete (dir, true); } catch { }
		}
	}
}
