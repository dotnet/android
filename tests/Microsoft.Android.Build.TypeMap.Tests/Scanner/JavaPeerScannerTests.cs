using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Android.Build.TypeMap.Tests;

public class JavaPeerScannerTests
{
	static string TestFixtureAssemblyPath {
		get {
			var testAssemblyDir = Path.GetDirectoryName (typeof (JavaPeerScannerTests).Assembly.Location)!;
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

	JavaPeerInfo FindByJavaName (List<JavaPeerInfo> peers, string javaName)
	{
		var peer = peers.FirstOrDefault (p => p.JavaName == javaName);
		Assert.NotNull (peer);
		return peer;
	}

	JavaPeerInfo FindByManagedName (List<JavaPeerInfo> peers, string managedName)
	{
		var peer = peers.FirstOrDefault (p => p.ManagedTypeName == managedName);
		Assert.NotNull (peer);
		return peer;
	}

	[Fact]
	public void Scan_FindsAllJavaPeerTypes ()
	{
		var peers = ScanFixtures ();
		Assert.NotEmpty (peers);
		// MCW types with [Register]
		Assert.Contains (peers, p => p.JavaName == "java/lang/Object");
		Assert.Contains (peers, p => p.JavaName == "android/app/Activity");
		// User type with JNI name from [Activity(Name="...")]
		Assert.Contains (peers, p => p.JavaName == "my/app/MainActivity");
		// Exception/Throwable hierarchy
		Assert.Contains (peers, p => p.JavaName == "java/lang/Throwable");
		Assert.Contains (peers, p => p.JavaName == "java/lang/Exception");
	}

	[Fact]
	public void Scan_McwTypes_HaveDoNotGenerateAcw ()
	{
		var peers = ScanFixtures ();

		var activity = FindByJavaName (peers, "android/app/Activity");
		Assert.True (activity.DoNotGenerateAcw, "Activity should be MCW (DoNotGenerateAcw=true)");

		var button = FindByJavaName (peers, "android/widget/Button");
		Assert.True (button.DoNotGenerateAcw, "Button should be MCW");
	}

	[Fact]
	public void Scan_UserTypes_DoNotGenerateAcwIsFalse ()
	{
		var peers = ScanFixtures ();

		var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
		Assert.False (mainActivity.DoNotGenerateAcw, "MainActivity should not have DoNotGenerateAcw");
	}

	[Fact]
	public void Scan_ActivityType_IsUnconditional ()
	{
		var peers = ScanFixtures ();
		var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
		Assert.True (mainActivity.IsUnconditional, "MainActivity with [Activity] should be unconditional");
	}

	[Fact]
	public void Scan_ServiceType_IsUnconditional ()
	{
		var peers = ScanFixtures ();
		var service = FindByJavaName (peers, "my/app/MyService");
		Assert.True (service.IsUnconditional, "MyService with [Service] should be unconditional");
	}

	[Fact]
	public void Scan_BroadcastReceiverType_IsUnconditional ()
	{
		var peers = ScanFixtures ();
		var receiver = FindByJavaName (peers, "my/app/MyReceiver");
		Assert.True (receiver.IsUnconditional, "MyReceiver with [BroadcastReceiver] should be unconditional");
	}

	[Fact]
	public void Scan_ContentProviderType_IsUnconditional ()
	{
		var peers = ScanFixtures ();
		var provider = FindByJavaName (peers, "my/app/MyProvider");
		Assert.True (provider.IsUnconditional, "MyProvider with [ContentProvider] should be unconditional");
	}

	[Fact]
	public void Scan_TypeWithoutComponentAttribute_IsTrimmable ()
	{
		var peers = ScanFixtures ();
		var helper = FindByJavaName (peers, "my/app/MyHelper");
		Assert.False (helper.IsUnconditional, "MyHelper without component attr should be trimmable");
	}

	[Fact]
	public void Scan_McwBinding_IsTrimmable ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		Assert.False (activity.IsUnconditional, "MCW Activity should be trimmable (no component attr on MCW type)");
	}

	[Fact]
	public void Scan_InterfaceType_IsMarkedAsInterface ()
	{
		var peers = ScanFixtures ();
		var listener = FindByManagedName (peers, "Android.Views.IOnClickListener");
		Assert.True (listener.IsInterface, "IOnClickListener should be marked as interface");
	}

	[Fact]
	public void Scan_InvokerTypes_AreIncluded ()
	{
		var peers = ScanFixtures ();
		var invoker = peers.FirstOrDefault (p => p.ManagedTypeName == "Android.Views.IOnClickListenerInvoker");
		Assert.NotNull (invoker);
		Assert.True (invoker.DoNotGenerateAcw, "Invoker should have DoNotGenerateAcw=true");
		Assert.Equal ("android/view/View$OnClickListener", invoker.JavaName);
	}

	[Fact]
	public void Scan_GenericType_IsGenericDefinition ()
	{
		var peers = ScanFixtures ();
		var generic = FindByJavaName (peers, "my/app/GenericHolder");
		Assert.True (generic.IsGenericDefinition, "GenericHolder<T> should be marked as generic definition");
	}

	[Fact]
	public void Scan_AbstractType_IsMarkedAbstract ()
	{
		var peers = ScanFixtures ();
		var abstractBase = FindByJavaName (peers, "my/app/AbstractBase");
		Assert.True (abstractBase.IsAbstract, "AbstractBase should be marked as abstract");
	}

	[Fact]
	public void Scan_MarshalMethods_Collected ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		Assert.NotEmpty (activity.MarshalMethods);
	}

	[Fact]
	public void Scan_UserTypeOverride_CollectsMarshalMethods ()
	{
		var peers = ScanFixtures ();
		var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
		Assert.NotEmpty (mainActivity.MarshalMethods);

		var onCreate = mainActivity.MarshalMethods.FirstOrDefault (m => m.ManagedMethodName == "OnCreate");
		Assert.NotNull (onCreate);
		Assert.Equal ("(Landroid/os/Bundle;)V", onCreate.JniSignature);
	}

	[Fact]
	public void Scan_TypeWithOwnActivationCtor_ResolvesToSelf ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		Assert.NotNull (activity.ActivationCtor);
		Assert.Equal ("Android.App.Activity", activity.ActivationCtor.DeclaringTypeName);
		Assert.Equal (ActivationCtorStyle.XamarinAndroid, activity.ActivationCtor.Style);
	}

	[Fact]
	public void Scan_TypeWithoutOwnActivationCtor_InheritsFromBase ()
	{
		var peers = ScanFixtures ();
		var simpleActivity = FindByJavaName (peers, "my/app/SimpleActivity");
		Assert.NotNull (simpleActivity.ActivationCtor);
		Assert.Equal ("Android.App.Activity", simpleActivity.ActivationCtor.DeclaringTypeName);
		Assert.Equal (ActivationCtorStyle.XamarinAndroid, simpleActivity.ActivationCtor.Style);
	}

	[Fact]
	public void Scan_TypeWithOwnActivationCtor_DoesNotLookAtBase ()
	{
		var peers = ScanFixtures ();
		var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
		Assert.NotNull (mainActivity.ActivationCtor);
		Assert.Equal ("Android.App.Activity", mainActivity.ActivationCtor.DeclaringTypeName);
	}

	[Fact]
	public void Scan_AllTypes_HaveAssemblyName ()
	{
		var peers = ScanFixtures ();
		Assert.All (peers, peer =>
			Assert.False (string.IsNullOrEmpty (peer.AssemblyName),
				$"Type {peer.ManagedTypeName} should have assembly name"));
	}

	[Fact]
	public void Scan_InvokerSharesJavaNameWithInterface ()
	{
		var peers = ScanFixtures ();
		var clickListenerPeers = peers.Where (p => p.JavaName == "android/view/View$OnClickListener").ToList ();
		// Interface + Invoker share the same JNI name (this is expected — they're aliases)
		Assert.Equal (2, clickListenerPeers.Count);
		Assert.Contains (clickListenerPeers, p => p.IsInterface);
		Assert.Contains (clickListenerPeers, p => p.DoNotGenerateAcw);
	}

	[Fact]
	public void Scan_ManagedTypeNamespace_IsCorrect ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		Assert.Equal ("Android.App", activity.ManagedTypeNamespace);
		Assert.Equal ("Activity", activity.ManagedTypeShortName);
	}

	[Fact]
	public void Scan_ActivityBaseJavaName_IsJavaLangObject ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		Assert.Equal ("java/lang/Object", activity.BaseJavaName);
	}

	[Fact]
	public void Scan_MainActivityBaseJavaName_IsActivity ()
	{
		var peers = ScanFixtures ();
		var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
		Assert.Equal ("android/app/Activity", mainActivity.BaseJavaName);
	}

	[Fact]
	public void Scan_JavaLangObjectBaseJavaName_IsNull ()
	{
		var peers = ScanFixtures ();
		var jlo = FindByJavaName (peers, "java/lang/Object");
		Assert.Null (jlo.BaseJavaName);
	}

	[Fact]
	public void Scan_TypeImplementingInterface_HasInterfaceJavaNames ()
	{
		var peers = ScanFixtures ();
		var clickable = FindByJavaName (peers, "my/app/ClickableView");
		Assert.Contains ("android/view/View$OnClickListener", clickable.ImplementedInterfaceJavaNames);
	}

	[Fact]
	public void Scan_TypeNotImplementingInterface_HasEmptyList ()
	{
		var peers = ScanFixtures ();
		var helper = FindByJavaName (peers, "my/app/MyHelper");
		Assert.Empty (helper.ImplementedInterfaceJavaNames);
	}

	[Fact]
	public void Scan_TypeWithRegisteredCtors_HasJavaConstructors ()
	{
		var peers = ScanFixtures ();
		var customView = FindByJavaName (peers, "my/app/CustomView");
		Assert.Equal (2, customView.JavaConstructors.Count);
		Assert.Equal ("()V", customView.JavaConstructors [0].JniSignature);
		Assert.Equal (0, customView.JavaConstructors [0].ConstructorIndex);
		Assert.Equal ("(Landroid/content/Context;)V", customView.JavaConstructors [1].JniSignature);
		Assert.Equal (1, customView.JavaConstructors [1].ConstructorIndex);
	}

	[Fact]
	public void Scan_TypeWithoutRegisteredCtors_HasEmptyJavaConstructors ()
	{
		var peers = ScanFixtures ();
		var helper = FindByJavaName (peers, "my/app/MyHelper");
		Assert.Empty (helper.JavaConstructors);
	}

	[Fact]
	public void Scan_MarshalMethod_HasDeclaringAssemblyName ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		Assert.All (activity.MarshalMethods, m =>
			Assert.Equal ("TestFixtures", m.DeclaringAssemblyName));
	}

	[Fact]
	public void Scan_MarshalMethod_HasNativeCallbackName ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		var onCreate = activity.MarshalMethods.FirstOrDefault (m => m.JniName == "onCreate");
		Assert.NotNull (onCreate);
		Assert.Equal ("n_OnCreate", onCreate.NativeCallbackName);
	}

	[Fact]
	public void Scan_MarshalMethod_JniNameIsJavaMethodName ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		var onCreate = activity.MarshalMethods.FirstOrDefault (m => m.ManagedMethodName == "OnCreate");
		Assert.NotNull (onCreate);
		Assert.Equal ("onCreate", onCreate.JniName);
	}

	[Fact]
	public void Scan_UserTypeWithActivityName_DiscoveredWithoutRegister ()
	{
		var peers = ScanFixtures ();
		var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
		Assert.False (mainActivity.DoNotGenerateAcw);
		Assert.True (mainActivity.IsUnconditional, "Types with [Activity] are unconditional");
	}

	[Fact]
	public void Scan_UserTypeWithServiceName_DiscoveredWithoutRegister ()
	{
		var peers = ScanFixtures ();
		var service = FindByJavaName (peers, "my/app/MyService");
		Assert.False (service.DoNotGenerateAcw);
		Assert.True (service.IsUnconditional);
	}

	[Fact]
	public void Scan_UserTypeWithBroadcastReceiverName_DiscoveredWithoutRegister ()
	{
		var peers = ScanFixtures ();
		var receiver = FindByJavaName (peers, "my/app/MyReceiver");
		Assert.False (receiver.DoNotGenerateAcw);
		Assert.True (receiver.IsUnconditional);
	}

	[Fact]
	public void Scan_UserTypeWithContentProviderName_DiscoveredWithoutRegister ()
	{
		var peers = ScanFixtures ();
		var provider = FindByJavaName (peers, "my/app/MyProvider");
		Assert.False (provider.DoNotGenerateAcw);
		Assert.True (provider.IsUnconditional);
	}

	[Fact]
	public void Scan_Throwable_IsMcwType ()
	{
		var peers = ScanFixtures ();
		var throwable = FindByJavaName (peers, "java/lang/Throwable");
		Assert.True (throwable.DoNotGenerateAcw);
		Assert.Equal ("java/lang/Object", throwable.BaseJavaName);
	}

	[Fact]
	public void Scan_Exception_ExtendsThrowable ()
	{
		var peers = ScanFixtures ();
		var exception = FindByJavaName (peers, "java/lang/Exception");
		Assert.True (exception.DoNotGenerateAcw);
		Assert.Equal ("java/lang/Throwable", exception.BaseJavaName);
	}

	[Fact]
	public void Scan_NestedType_IsDiscovered ()
	{
		var peers = ScanFixtures ();
		var inner = FindByJavaName (peers, "my/app/Outer$Inner");
		Assert.Equal ("MyApp.Outer+Inner", inner.ManagedTypeName);
	}

	[Fact]
	public void Scan_NestedTypeInInterface_IsDiscovered ()
	{
		var peers = ScanFixtures ();
		var result = FindByJavaName (peers, "my/app/ICallback$Result");
		Assert.Equal ("MyApp.ICallback+Result", result.ManagedTypeName);
	}

	[Fact]
	public void Scan_MarshalMethod_BoolReturn_HasCorrectJniSignature ()
	{
		var peers = ScanFixtures ();
		var handler = FindByJavaName (peers, "my/app/TouchHandler");
		var onTouch = handler.MarshalMethods.FirstOrDefault (m => m.JniName == "onTouch");
		Assert.NotNull (onTouch);
		Assert.Equal ("(Landroid/view/View;I)Z", onTouch.JniSignature);
		Assert.Equal ("Z", onTouch.JniReturnType);
	}

	[Fact]
	public void Scan_MarshalMethod_BoolParam_HasCorrectJniSignature ()
	{
		var peers = ScanFixtures ();
		var handler = FindByJavaName (peers, "my/app/TouchHandler");
		var onFocus = handler.MarshalMethods.FirstOrDefault (m => m.JniName == "onFocusChange");
		Assert.NotNull (onFocus);
		Assert.Equal ("(Landroid/view/View;Z)V", onFocus.JniSignature);
		Assert.Equal (2, onFocus.Parameters.Count);
		Assert.Equal ("Z", onFocus.Parameters [1].JniType);
	}

	[Fact]
	public void Scan_MarshalMethod_VoidReturn ()
	{
		var peers = ScanFixtures ();
		var handler = FindByJavaName (peers, "my/app/TouchHandler");
		var onFocus = handler.MarshalMethods.FirstOrDefault (m => m.JniName == "onFocusChange");
		Assert.NotNull (onFocus);
		Assert.Equal ("V", onFocus.JniReturnType);
	}

	[Fact]
	public void Scan_MarshalMethod_ObjectReturn ()
	{
		var peers = ScanFixtures ();
		var handler = FindByJavaName (peers, "my/app/TouchHandler");
		var getText = handler.MarshalMethods.FirstOrDefault (m => m.JniName == "getText");
		Assert.NotNull (getText);
		Assert.Equal ("Ljava/lang/String;", getText.JniReturnType);
	}

	[Fact]
	public void Scan_MarshalMethod_MultiplePrimitiveParams ()
	{
		var peers = ScanFixtures ();
		var handler = FindByJavaName (peers, "my/app/TouchHandler");
		var onScroll = handler.MarshalMethods.FirstOrDefault (m => m.JniName == "onScroll");
		Assert.NotNull (onScroll);
		Assert.Equal (4, onScroll.Parameters.Count);
		Assert.Equal ("I", onScroll.Parameters [0].JniType);
		Assert.Equal ("System.Int32", onScroll.Parameters [0].ManagedType);
		Assert.Equal ("F", onScroll.Parameters [1].JniType);
		Assert.Equal ("System.Single", onScroll.Parameters [1].ManagedType);
		Assert.Equal ("J", onScroll.Parameters [2].JniType);
		Assert.Equal ("System.Int64", onScroll.Parameters [2].ManagedType);
		Assert.Equal ("D", onScroll.Parameters [3].JniType);
		Assert.Equal ("System.Double", onScroll.Parameters [3].ManagedType);
	}

	[Fact]
	public void Scan_MarshalMethod_ArrayParam ()
	{
		var peers = ScanFixtures ();
		var handler = FindByJavaName (peers, "my/app/TouchHandler");
		var setItems = handler.MarshalMethods.FirstOrDefault (m => m.JniName == "setItems");
		Assert.NotNull (setItems);
		Assert.Equal ("([Ljava/lang/String;)V", setItems.JniSignature);
		Assert.Single (setItems.Parameters);
		Assert.Equal ("[Ljava/lang/String;", setItems.Parameters [0].JniType);
	}

	[Fact]
	public void Scan_ExportMethod_CollectedAsMarshalMethod ()
	{
		var peers = ScanFixtures ();
		var export = FindByJavaName (peers, "my/app/ExportExample");
		Assert.Single (export.MarshalMethods);
		var method = export.MarshalMethods [0];
		Assert.Equal ("myExportedMethod", method.JniName);
		Assert.Null (method.Connector);
	}

	[Fact]
	public void Scan_CustomView_DiscoveredAsRegularType ()
	{
		var peers = ScanFixtures ();
		var customView = FindByJavaName (peers, "my/app/CustomView");
		Assert.False (customView.IsUnconditional, "Custom views are not unconditional by attribute alone");
		Assert.Equal ("android/view/View", customView.BaseJavaName);
	}

	[Fact]
	public void Scan_ApplicationType_IsUnconditional ()
	{
		var peers = ScanFixtures ();
		var app = FindByJavaName (peers, "my/app/MyApplication");
		Assert.True (app.IsUnconditional, "[Application] should be unconditional");
	}

	[Fact]
	public void Scan_InstrumentationType_IsUnconditional ()
	{
		var peers = ScanFixtures ();
		var instr = FindByJavaName (peers, "my/app/MyInstrumentation");
		Assert.True (instr.IsUnconditional, "[Instrumentation] should be unconditional");
	}

	[Fact (Skip = "BackupAgent cross-reference forcing unconditional is not yet implemented in the scanner")]
	public void Scan_BackupAgent_ForcedUnconditional ()
	{
		// TODO: Add BackupAgent fixture type and verify it's forced unconditional
		// This requires the MSBuild task to propagate the cross-reference.
	}

	[Fact]
	public void Scan_DeepHierarchy_ResolvesBaseJavaName ()
	{
		var peers = ScanFixtures ();
		var myButton = FindByJavaName (peers, "my/app/MyButton");
		Assert.Equal ("android/widget/Button", myButton.BaseJavaName);
	}

	[Fact]
	public void Scan_DeepHierarchy_InheritsActivationCtor ()
	{
		var peers = ScanFixtures ();
		var myButton = FindByJavaName (peers, "my/app/MyButton");
		Assert.NotNull (myButton.ActivationCtor);
		// MyButton → Button → View → Java.Lang.Object — should find XI ctor from View or Object
		Assert.Equal (ActivationCtorStyle.XamarinAndroid, myButton.ActivationCtor.Style);
	}

	[Fact]
	public void Scan_MultipleInterfaces_AllResolved ()
	{
		var peers = ScanFixtures ();
		var multi = FindByJavaName (peers, "my/app/MultiInterfaceView");
		Assert.Contains ("android/view/View$OnClickListener", multi.ImplementedInterfaceJavaNames);
		Assert.Contains ("android/view/View$OnLongClickListener", multi.ImplementedInterfaceJavaNames);
		Assert.Equal (2, multi.ImplementedInterfaceJavaNames.Count);
	}

	[Fact]
	public void Scan_AbstractMethod_CollectedAsMarshalMethod ()
	{
		var peers = ScanFixtures ();
		var abstractBase = FindByJavaName (peers, "my/app/AbstractBase");
		var doWork = abstractBase.MarshalMethods.FirstOrDefault (m => m.JniName == "doWork");
		Assert.NotNull (doWork);
		Assert.Equal ("()V", doWork.JniSignature);
	}

	[Fact]
	public void Scan_PropertyRegister_CollectedAsMarshalMethod ()
	{
		var peers = ScanFixtures ();
		var throwable = FindByJavaName (peers, "java/lang/Throwable");
		var getMessage = throwable.MarshalMethods.FirstOrDefault (m => m.JniName == "getMessage");
		Assert.NotNull (getMessage);
		Assert.Equal ("()Ljava/lang/String;", getMessage.JniSignature);
	}

	[Fact]
	public void Scan_MethodWithEmptyConnector_Collected ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		var onStart = activity.MarshalMethods.FirstOrDefault (m => m.JniName == "onStart");
		Assert.NotNull (onStart);
		Assert.Equal ("", onStart.Connector);
	}

	[Fact]
	public void Scan_InvokerWithRegisterAndDoNotGenerateAcw_IsIncluded ()
	{
		var peers = ScanFixtures ();
		// IOnClickListenerInvoker has [Register("android/view/View$OnClickListener", DoNotGenerateAcw=true)]
		// It should be included in the scanner output — generators will filter it later
		var invoker = peers.FirstOrDefault (p => p.ManagedTypeName == "Android.Views.IOnClickListenerInvoker");
		Assert.NotNull (invoker);
		Assert.True (invoker.DoNotGenerateAcw);
		Assert.Equal ("android/view/View$OnClickListener", invoker.JavaName);
	}

	[Fact]
	public void Scan_Interface_HasInvokerTypeNameFromRegisterConnector ()
	{
		var peers = ScanFixtures ();
		var listener = FindByManagedName (peers, "Android.Views.IOnClickListener");
		Assert.NotNull (listener.InvokerTypeName);
		Assert.Equal ("Android.Views.IOnClickListenerInvoker", listener.InvokerTypeName);
	}

	[Fact]
	public void Scan_Interface_IsNotMarkedDoNotGenerateAcw ()
	{
		var peers = ScanFixtures ();
		var listener = FindByManagedName (peers, "Android.Views.IOnClickListener");
		// Interfaces have [Register("name", "", "connector")] — the 3-arg form doesn't set DoNotGenerateAcw
		Assert.False (listener.DoNotGenerateAcw, "Interfaces should not have DoNotGenerateAcw");
	}

	[Fact]
	public void Scan_InterfaceMethod_CollectedAsMarshalMethod ()
	{
		var peers = ScanFixtures ();
		var listener = FindByManagedName (peers, "Android.Views.IOnClickListener");
		var onClick = listener.MarshalMethods.FirstOrDefault (m => m.JniName == "onClick");
		Assert.NotNull (onClick);
		Assert.Equal ("(Landroid/view/View;)V", onClick.JniSignature);
	}

	[Fact]
	public void Scan_GenericType_HasCorrectManagedTypeName ()
	{
		var peers = ScanFixtures ();
		var generic = FindByJavaName (peers, "my/app/GenericHolder");
		Assert.Equal ("MyApp.Generic.GenericHolder`1", generic.ManagedTypeName);
	}

	[Fact]
	public void Scan_Context_IsDiscovered ()
	{
		var peers = ScanFixtures ();
		var context = FindByJavaName (peers, "android/content/Context");
		Assert.True (context.DoNotGenerateAcw, "Context is MCW");
		Assert.Equal ("java/lang/Object", context.BaseJavaName);
	}
}
