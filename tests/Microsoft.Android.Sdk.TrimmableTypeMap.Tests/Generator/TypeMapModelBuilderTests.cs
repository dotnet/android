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
		public void Build_ComputesIgnoresAccessChecksToFromCrossAssemblyCallbacks ()
		{
			var peer = MakeAcwPeer ("my/app/MainActivity", "MyApp.MainActivity", "MyApp");
			((List<MarshalMethodInfo>) peer.MarshalMethods).Add (new MarshalMethodInfo {
				JniName = "onCreate",
				NativeCallbackName = "n_OnCreate",
				JniSignature = "(Landroid/os/Bundle;)V",
				IsConstructor = false,
				DeclaringTypeName = "Android.App.Activity",
				DeclaringAssemblyName = "Mono.Android",
			});
			var model = BuildModel (new [] { peer });
			// The UCO callback type references Mono.Android, which is cross-assembly
			Assert.Contains ("Mono.Android", model.IgnoresAccessChecksTo);
			// The output assembly itself should not appear
			Assert.DoesNotContain (model.AssemblyName, model.IgnoresAccessChecksTo);
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
			var peer = MakeInterfacePeer ();

			var model = BuildModel (new [] { peer });
			Assert.Single (model.ProxyTypes);
			var proxy = model.ProxyTypes [0];
			Assert.NotNull (proxy.InvokerType);
			Assert.Equal ("Android.Views.View+IOnClickListenerInvoker", proxy.InvokerType!.ManagedTypeName);
		}

		[Fact]
		public void Build_ProxyNaming_ReplacesDotAndPlus ()
		{
			var peer = MakePeerWithActivation ("com/example/Outer$Inner", "Com.Example.Outer.Inner", "App");
			var model = BuildModel (new [] { peer });

			Assert.Single (model.ProxyTypes);
			Assert.Equal ("Com_Example_Outer_Inner_Proxy", model.ProxyTypes [0].TypeName);
		}

	}

	public class AcwDetection
	{

		[Fact]
		public void Build_AcwType_IsAcwTrue ()
		{
			var peer = MakeAcwPeer ("my/app/Main", "MyApp.MainActivity", "App");
			var model = BuildModel (new [] { peer });

			Assert.Single (model.ProxyTypes);
			Assert.True (model.ProxyTypes [0].IsAcw);
		}

		[Fact]
		public void Build_McwType_IsAcwFalse ()
		{
			var peer = MakePeerWithActivation ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
			var model = BuildModel (new [] { peer });

			Assert.Single (model.ProxyTypes);
			Assert.False (model.ProxyTypes [0].IsAcw);
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

	}

	public class UcoMethods
	{

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

	}

	public class UcoConstructors
	{

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

	}

	public class NativeRegistrations
	{

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
			Assert.Equal ("()V", ctorReg.JniSignature);
			Assert.Equal ("nctor_0_uco", ctorReg.WrapperMethodName);
		}

		[Fact]
		public void Build_NativeRegistrations_ParameterizedConstructor_HasCorrectJniSignature ()
		{
			var peer = MakeAcwPeer ("my/app/MyView", "MyApp.MyView", "App");
			peer.JavaConstructors = new List<JavaConstructorInfo> {
				new JavaConstructorInfo { ConstructorIndex = 0, JniSignature = "()V" },
				new JavaConstructorInfo { ConstructorIndex = 1, JniSignature = "(Landroid/content/Context;)V",
					Parameters = new List<JniParameterInfo> {
						new JniParameterInfo { JniType = "Landroid/content/Context;" },
					}
				},
			};
			peer.MarshalMethods = new List<MarshalMethodInfo> {
				MakeMarshalMethod ("<init>", "n_ctor", "()V", isConstructor: true),
				MakeMarshalMethod ("<init>", "n_ctor", "(Landroid/content/Context;)V", isConstructor: true),
			};

			var model = BuildModel (new [] { peer });
			var proxy = model.ProxyTypes [0];

			var ctorRegs = proxy.NativeRegistrations.Where (r => r.JniMethodName.StartsWith ("nctor_")).ToList ();
			Assert.Equal (2, ctorRegs.Count);

			Assert.Equal ("()V", ctorRegs [0].JniSignature);
			Assert.Equal ("(Landroid/content/Context;)V", ctorRegs [1].JniSignature);
		}

		[Fact]
		public void Build_NonAcwProxy_NoNativeRegistrations ()
		{
			var peer = MakePeerWithActivation ("java/lang/Object", "Java.Lang.Object", "Mono.Android");
			var model = BuildModel (new [] { peer });

			Assert.Single (model.ProxyTypes);
			Assert.Empty (model.ProxyTypes [0].NativeRegistrations);
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

		[Fact]
		public void Build_FromScannedFixtures_AcwTypesHaveUcoMethods ()
		{
			var peers = ScanFixtures ();
			var model = BuildModel (peers);

			var acwProxies = model.ProxyTypes.Where (p => p.IsAcw).ToList ();
			Assert.NotEmpty (acwProxies);

			foreach (var proxy in acwProxies) {
				Assert.NotEmpty (proxy.NativeRegistrations);
			}
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
			Assert.False (model.Entries [0].IsUnconditional);
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
			// MCW types with DoNotGenerateAcw → not ACW
			Assert.False (proxy.IsAcw);
			Assert.Empty (proxy.UcoMethods);
			Assert.Empty (proxy.UcoConstructors);
			Assert.Empty (proxy.NativeRegistrations);
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

	public class FixtureAcwTypes
	{

		[Fact]
		public void Fixture_MainActivity_IsAcw ()
		{
			var peer = FindFixtureByJavaName ("my/app/MainActivity");
			Assert.False (peer.DoNotGenerateAcw);
			Assert.NotEmpty (peer.MarshalMethods);
			Assert.NotNull (peer.ActivationCtor);

			var model = BuildModel (new [] { peer }, "TypeMap");
			var proxy = FindProxy (model, "MyApp_MainActivity_Proxy");
			Assert.NotNull (proxy);
			Assert.True (proxy!.IsAcw);
			Assert.True (proxy.HasActivation);
		}

		[Fact]
		public void Fixture_MainActivity_UcoMethods ()
		{
			var peer = FindFixtureByJavaName ("my/app/MainActivity");
			var model = BuildModel (new [] { peer }, "TypeMap");
			var proxy = FindProxy (model, "MyApp_MainActivity_Proxy")!;

			var nonCtorMethods = peer.MarshalMethods.Where (m => !m.IsConstructor).ToList ();
			Assert.Equal (nonCtorMethods.Count, proxy.UcoMethods.Count);

			var onCreateUco = proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnCreate");
			Assert.NotNull (onCreateUco);
			Assert.Equal ("(Landroid/os/Bundle;)V", onCreateUco!.JniSignature);
			Assert.StartsWith ("n_onCreate_uco_", onCreateUco.WrapperName);
		}

	}

	public class FixtureTouchHandler
	{

		[Fact]
		public void Fixture_TouchHandler_AllUcoMethods ()
		{
			var peer = FindFixtureByJavaName ("my/app/TouchHandler");
			var model = BuildModel (new [] { peer }, "TypeMap");
			var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "MyApp_TouchHandler_Proxy");
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

	}

	public class FixtureCustomView
	{

		[Fact]
		public void Fixture_CustomView_HasTwoConstructorWrappers ()
		{
			var peer = FindFixtureByJavaName ("my/app/CustomView");

			var model = BuildModel (new [] { peer }, "TypeMap");
			var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "MyApp_CustomView_Proxy");
			Assert.NotNull (proxy);

			if (proxy!.IsAcw) {
				Assert.Equal (2, proxy.UcoConstructors.Count);
				Assert.Equal ("nctor_0_uco", proxy.UcoConstructors [0].WrapperName);
				Assert.Equal ("nctor_1_uco", proxy.UcoConstructors [1].WrapperName);
				Assert.Equal ("MyApp.CustomView", proxy.UcoConstructors [0].TargetType.ManagedTypeName);
				Assert.Equal ("MyApp.CustomView", proxy.UcoConstructors [1].TargetType.ManagedTypeName);

				// Constructor JNI signatures should be propagated
				Assert.Equal ("()V", proxy.UcoConstructors [0].JniSignature);
				Assert.Equal ("(Landroid/content/Context;)V", proxy.UcoConstructors [1].JniSignature);

				// Constructor registrations must use the actual JNI signatures
				var ctorRegs = proxy.NativeRegistrations.Where (r => r.JniMethodName.StartsWith ("nctor_")).ToList ();
				Assert.Equal (2, ctorRegs.Count);
				Assert.Equal ("()V", ctorRegs [0].JniSignature);
				Assert.Equal ("(Landroid/content/Context;)V", ctorRegs [1].JniSignature);
			}
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
			var ifacePeer = MakeInterfacePeer ("my/app/IFoo", "MyApp.IFoo", "App", "MyApp.FooInvoker");
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

			if (peer.ActivationCtor != null && peer.MarshalMethods.Count > 0) {
				var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == expectedProxyName);
				Assert.NotNull (proxy);
				Assert.True (proxy!.IsAcw);
			}
		}

		[Fact]
		public void Fixture_ClickableView_HasOnClickUcoWrapper ()
		{
			var peer = FindFixtureByJavaName ("my/app/ClickableView");
			var model = BuildModel (new [] { peer }, "TypeMap");

			if (peer.ActivationCtor != null && peer.MarshalMethods.Count > 0) {
				var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "MyApp_ClickableView_Proxy");
				Assert.NotNull (proxy);
				var onClick = proxy!.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnClick");
				Assert.NotNull (onClick);
				Assert.Equal ("(Landroid/view/View;)V", onClick!.JniSignature);
			}
		}

		[Fact]
		public void Fixture_MultiInterfaceView_HasAllUcoMethods ()
		{
			var peer = FindFixtureByJavaName ("my/app/MultiInterfaceView");
			var model = BuildModel (new [] { peer }, "TypeMap");

			if (peer.ActivationCtor != null && peer.MarshalMethods.Count > 0) {
				var proxy = model.ProxyTypes.FirstOrDefault (p => p.TypeName == "MyApp_MultiInterfaceView_Proxy");
				Assert.NotNull (proxy);
				Assert.NotNull (proxy!.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnClick"));
				Assert.NotNull (proxy.UcoMethods.FirstOrDefault (u => u.CallbackMethodName == "n_OnLongClick"));
			}
		}

	}

	public class FixtureImplementorsAndDispatchers
	{

		[Theory]
		[InlineData ("android/view/View_IOnClickListenerImplementor", "Implementor")]
		[InlineData ("android/view/View_ClickEventDispatcher", "EventDispatcher")]
		public void Fixture_HelperType_IsTrimmable_NotUnconditional (string javaName, string kind)
		{
			var peer = FindFixtureByJavaName (javaName);
			Assert.False (peer.DoNotGenerateAcw);
			Assert.False (peer.IsInterface);

			var model = BuildModel (new [] { peer }, "TypeMap");

			var entry = model.Entries.FirstOrDefault ();
			Assert.NotNull (entry);
			Assert.False (entry!.IsUnconditional, $"{kind} should NOT be unconditional");
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
			var ifacePeer = MakeInterfacePeer ("my/app/MyInvoker", "MyApp.IMyInterface", "App", "MyApp.MyInvoker");
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

				int expected = model.Entries.Count + model.IgnoresAccessChecksTo.Count;
				Assert.Equal (expected, totalAttrs);
			});
		}

		[Fact]
		public void FullPipeline_TouchHandler_AcwProxyHasUcoAttributes ()
		{
			var peer = FindFixtureByJavaName ("my/app/TouchHandler");
			var model = BuildModel (new [] { peer }, "UcoAttrTest");

			EmitAndVerify (model, "UcoAttrTest", (pe, reader) => {
				var proxy = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.First (t => reader.GetString (t.Name) == "MyApp_TouchHandler_Proxy");

				var methods = proxy.GetMethods ()
					.Select (h => reader.GetMethodDefinition (h))
					.ToList ();

				var ucoMethods = methods.Where (m => reader.GetString (m.Name).Contains ("_uco_")).ToList ();
				Assert.NotEmpty (ucoMethods);

				foreach (var uco in ucoMethods) {
					var attrs = uco.GetCustomAttributes ().Select (h => reader.GetCustomAttribute (h)).ToList ();
					Assert.NotEmpty (attrs);
				}
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
				Assert.Contains ("get_TargetType", methodNames);

				if (model.ProxyTypes [0].IsAcw) {
					Assert.Contains ("RegisterNatives", methodNames);
					Assert.Contains (methodNames, m => m.StartsWith ("nctor_") && m.EndsWith ("_uco"));
				}
			});
		}

		[Fact]
		public void FullPipeline_CustomView_UcoConstructorMatchesJniSignature ()
		{
			var peer = FindFixtureByJavaName ("my/app/CustomView");
			var model = BuildModel (new [] { peer }, "CtorSigTest");

			EmitAndVerify (model, "CtorSigTest", (pe, reader) => {
				var proxy = reader.TypeDefinitions
					.Select (h => reader.GetTypeDefinition (h))
					.First (t => reader.GetString (t.Name) == "MyApp_CustomView_Proxy");

				var ucoCtors = proxy.GetMethods ()
					.Select (h => reader.GetMethodDefinition (h))
					.Where (m => reader.GetString (m.Name).StartsWith ("nctor_") && reader.GetString (m.Name).EndsWith ("_uco"))
					.ToList ();

				Assert.NotEmpty (ucoCtors);

				foreach (var uco in ucoCtors) {
					var name = reader.GetString (uco.Name);
					var modelUco = model.ProxyTypes
						.SelectMany (p => p.UcoConstructors)
						.First (u => u.WrapperName == name);

					// UCO constructor signature: jnienv + self + JNI params
					int expectedJniParams = JniSignatureHelper.ParseParameterTypes (modelUco.JniSignature).Count;
					int expectedTotal = 2 + expectedJniParams;

					var sig = reader.GetBlobReader (uco.Signature);
					var header = sig.ReadSignatureHeader ();
					int paramCount = sig.ReadCompressedInteger ();
					Assert.Equal (expectedTotal, paramCount);
				}
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
			// java/lang/Object → essential → 2-arg unconditional
			var objectPeer = FindFixtureByJavaName ("java/lang/Object");
			// android/app/Activity → MCW → 3-arg trimmable
			var activityPeer = FindFixtureByJavaName ("android/app/Activity");

			var model = BuildModel (new [] { objectPeer, activityPeer }, "MixedBlob");
			Assert.Equal (2, model.Entries.Count);

			EmitAndVerify (model, "MixedBlob", (pe, reader) => {
				var attrs = ReadAllTypeMapAttributeBlobs (reader);
				Assert.Equal (2, attrs.Count);

				var unconditional = attrs.FirstOrDefault (a => a.jniName == "java/lang/Object");
				Assert.NotNull (unconditional.jniName);
				Assert.Null (unconditional.targetRef);

				var trimmable = attrs.FirstOrDefault (a => a.jniName == "android/app/Activity");
				Assert.NotNull (trimmable.jniName);
				Assert.NotNull (trimmable.targetRef);
				Assert.Contains ("Android.App.Activity", trimmable.targetRef!);
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
		public void FullPipeline_McwBinding_Emits3ArgAttribute ()
		{
			// android/app/Activity is MCW → trimmable 3-arg attribute
			var peer = FindFixtureByJavaName ("android/app/Activity");
			var model = BuildModel (new [] { peer }, "Blob3Arg");
			Assert.Single (model.Entries);
			Assert.False (model.Entries [0].IsUnconditional);

			EmitAndVerify (model, "Blob3Arg", (pe, reader) => {
				var (jniName, proxyRef, targetRef) = ReadFirstTypeMapAttributeBlob (reader);

				Assert.Equal ("android/app/Activity", jniName);
				Assert.NotNull (proxyRef);
				Assert.Contains ("Android_App_Activity_Proxy", proxyRef!);
				Assert.NotNull (targetRef);
				Assert.Contains ("Android.App.Activity", targetRef!);
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

	static void EmitAndVerify (TypeMapAssemblyData model, string assemblyName, Action<PEReader, MetadataReader> verify)
	{
		var outputDir = CreateTempDir ();
		try {
			var outputPath = Path.Combine (outputDir, $"{assemblyName}.dll");
			var emitter = new TypeMapAssemblyEmitter (new Version (11, 0, 0, 0));
			emitter.Emit (model, outputPath);
			using var pe = new PEReader (File.OpenRead (outputPath));
			verify (pe, pe.GetMetadataReader ());
		} finally {
			DeleteTempDir (outputDir);
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
}