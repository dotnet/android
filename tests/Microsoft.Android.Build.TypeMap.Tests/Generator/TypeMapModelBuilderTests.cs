using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.Android.Build.TypeMap.Tests;

public class TypeMapModelBuilderTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (TypeMapModelBuilderTests).Assembly.Location)!;
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

	TypeMapAssemblyModel BuildModel (IReadOnlyList<JavaPeerInfo> peers, string? assemblyName = null)
	{
		var outputPath = Path.Combine ("/tmp", (assemblyName ?? "TestTypeMap") + ".dll");
		var builder = new TypeMapModelBuilder ();
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
		var builder = new TypeMapModelBuilder ();
		var model = builder.Build (Array.Empty<JavaPeerInfo> (), "/some/path/Foo.Bar.dll");
		Assert.Equal ("Foo.Bar", model.AssemblyName);
		Assert.Equal ("Foo.Bar.dll", model.ModuleName);
	}

	[Fact]
	public void Build_ExplicitAssemblyName_OverridesOutputPath ()
	{
		var builder = new TypeMapModelBuilder ();
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
}
