using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

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
	public void Scan_TypeWithRegisteredCtors_HasConstructorMarshalMethods ()
	{
		var peers = ScanFixtures ();
		var customView = FindByJavaName (peers, "my/app/CustomView");
		var ctors = customView.MarshalMethods.Where (m => m.IsConstructor).ToList ();
		Assert.Equal (2, ctors.Count);
		Assert.Equal ("()V", ctors [0].JniSignature);
		Assert.Equal ("(Landroid/content/Context;)V", ctors [1].JniSignature);
	}

	[Fact]
	public void Scan_TypeWithoutRegisteredCtors_HasNoConstructorMarshalMethods ()
	{
		var peers = ScanFixtures ();
		var helper = FindByJavaName (peers, "my/app/MyHelper");
		Assert.DoesNotContain (helper.MarshalMethods, m => m.IsConstructor);
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
	}

	[Fact]
	public void Scan_MarshalMethod_BoolParam_HasCorrectJniSignature ()
	{
		var peers = ScanFixtures ();
		var handler = FindByJavaName (peers, "my/app/TouchHandler");
		var onFocus = handler.MarshalMethods.FirstOrDefault (m => m.JniName == "onFocusChange");
		Assert.NotNull (onFocus);
		Assert.Equal ("(Landroid/view/View;Z)V", onFocus.JniSignature);
	}

	[Fact]
	public void Scan_MarshalMethod_MultiplePrimitiveParams ()
	{
		var peers = ScanFixtures ();
		var handler = FindByJavaName (peers, "my/app/TouchHandler");
		var onScroll = handler.MarshalMethods.FirstOrDefault (m => m.JniName == "onScroll");
		Assert.NotNull (onScroll);
		Assert.Equal ("(IFJD)V", onScroll.JniSignature);
	}

	[Fact]
	public void Scan_MarshalMethod_ArrayParam ()
	{
		var peers = ScanFixtures ();
		var handler = FindByJavaName (peers, "my/app/TouchHandler");
		var setItems = handler.MarshalMethods.FirstOrDefault (m => m.JniName == "setItems");
		Assert.NotNull (setItems);
		Assert.Equal ("([Ljava/lang/String;)V", setItems.JniSignature);
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
	public void Scan_ExportConstructor_CollectedWithNullConnector ()
	{
		var peers = ScanFixtures ();
		var peer = FindByJavaName (peers, "my/app/ExportsConstructors");
		// [Export] on constructors: 2 exported ctors + 0 Register methods
		var exportCtors = peer.MarshalMethods.Where (m => m.IsConstructor && m.Connector == null).ToList ();
		Assert.Equal (2, exportCtors.Count);
		// Verify one is parameterless, one takes int
		Assert.Contains (exportCtors, c => c.JniSignature == "()V");
		Assert.Contains (exportCtors, c => c.JniSignature == "(I)V");
	}

	[Fact]
	public void Scan_ExportMethodWithParams_HasCorrectJniSignature ()
	{
		var peers = ScanFixtures ();
		var peer = FindByJavaName (peers, "my/app/ExportMethodWithParams");
		Assert.Equal (2, peer.MarshalMethods.Count);
		// All should be [Export] (null connector)
		Assert.All (peer.MarshalMethods, m => Assert.Null (m.Connector));
		// doWork(int) → (I)V
		var doWork = peer.MarshalMethods.First (m => m.JniName == "doWork");
		Assert.Equal ("(I)V", doWork.JniSignature);
		// computeName(String, int) → (Ljava/lang/String;I)Ljava/lang/String;
		var computeName = peer.MarshalMethods.First (m => m.JniName == "computeName");
		Assert.Equal ("(Ljava/lang/String;I)Ljava/lang/String;", computeName.JniSignature);
	}

	[Fact]
	public void Scan_ExportMembersComprehensive_NameOverrideAndThrows ()
	{
		var peers = ScanFixtures ();
		var peer = FindByJavaName (peers, "my/app/ExportMembersComprehensive");
		Assert.Equal (4, peer.MarshalMethods.Count);

		// [Export("attributeOverridesNames")] on CompletelyDifferentName
		var renamed = peer.MarshalMethods.First (m => m.JniName == "attributeOverridesNames");
		Assert.Null (renamed.Connector);
		Assert.Equal ("n_CompletelyDifferentName", renamed.NativeCallbackName);

		// [Export(ThrownNames = new [] { "java.lang.Throwable" })]
		var throwing = peer.MarshalMethods.First (m => m.JniName == "methodThatThrows");
		Assert.NotNull (throwing.ThrownNames);
		Assert.Single (throwing.ThrownNames!);
		Assert.Equal ("java.lang.Throwable", throwing.ThrownNames! [0]);

		// [Export(ThrownNames = new string [0])]
		var emptyThrows = peer.MarshalMethods.First (m => m.JniName == "methodThatThrowsEmptyArray");
		// Empty array should result in null or empty ThrownNames
		Assert.True (emptyThrows.ThrownNames == null || emptyThrows.ThrownNames.Count == 0);
	}

	[Fact]
	public void Scan_ExportCtorWithSuperArgs_HasSuperArgumentsString ()
	{
		var peers = ScanFixtures ();
		var peer = FindByJavaName (peers, "my/app/ExportCtorWithSuperArgs");
		var ctor = peer.MarshalMethods.FirstOrDefault (m => m.IsConstructor);
		Assert.NotNull (ctor);
		Assert.Equal ("", ctor!.SuperArgumentsString);
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

	[Fact]
	public void Scan_BackupAgent_ForcedUnconditional ()
	{
		var peers = ScanFixtures ();
		var backupAgent = FindByJavaName (peers, "my/app/MyBackupAgent");
		Assert.True (backupAgent.IsUnconditional, "BackupAgent referenced from [Application] should be forced unconditional");
	}

	[Fact]
	public void Scan_ManageSpaceActivity_ForcedUnconditional ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "my/app/MyManageSpaceActivity");
		Assert.True (activity.IsUnconditional, "ManageSpaceActivity referenced from [Application] should be forced unconditional");
	}

	[Fact]
	public void Scan_BackupAgent_NotUnconditional_WhenNotReferenced ()
	{
		// A type extending BackupAgent that is NOT referenced from [Application]
		// should remain trimmable (not unconditional).
		var peers = ScanFixtures ();
		var helper = FindByJavaName (peers, "my/app/MyHelper");
		Assert.False (helper.IsUnconditional, "Unreferenced type should remain trimmable");
	}

	[Fact]
	public void Scan_McwBinding_CompatJniNameEqualsJavaName ()
	{
		var peers = ScanFixtures ();
		var activity = FindByJavaName (peers, "android/app/Activity");
		Assert.Equal (activity.JavaName, activity.CompatJniName);
	}

	[Fact]
	public void Scan_UserTypeWithRegister_CompatJniNameEqualsJavaName ()
	{
		var peers = ScanFixtures ();
		var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
		Assert.Equal (mainActivity.JavaName, mainActivity.CompatJniName);
	}

	[Fact]
	public void Scan_UserTypeWithoutRegister_CompatJniNameUsesRawNamespace ()
	{
		var peers = ScanFixtures ();
		var unregistered = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.UnregisteredHelper");
		Assert.NotNull (unregistered);

		// JavaName should use CRC64 package
		Assert.StartsWith ("crc64", unregistered.JavaName);

		// CompatJniName should use the raw namespace
		Assert.Equal ("myapp/UnregisteredHelper", unregistered.CompatJniName);
	}

	[Fact]
	public void Scan_CustomJniNameProviderAttribute_UsesNameFromAttribute ()
	{
		var peers = ScanFixtures ();
		var widget = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.CustomWidget");
		Assert.NotNull (widget);

		// The custom attribute provides the JNI name via IJniNameProviderAttribute
		Assert.Equal ("com/example/CustomWidget", widget.JavaName);
		Assert.Equal ("com/example/CustomWidget", widget.CompatJniName);
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

	// ================================================================
	// Edge case tests — discovered during side-by-side testing
	// ================================================================

	[Fact]
	public void Scan_GenericBaseType_ResolvesViaTypeSpecification ()
	{
		// ConcreteFromGeneric extends GenericBase<string>. The base type
		// is a TypeSpecification (generic instantiation). The scanner must
		// decode the blob to resolve the underlying TypeDef/TypeRef.
		var peers = ScanFixtures ();
		var concrete = FindByJavaName (peers, "my/app/ConcreteFromGeneric");
		Assert.Equal ("my/app/GenericBase", concrete.BaseJavaName);
	}

	[Fact]
	public void Scan_GenericInterface_ResolvesViaTypeSpecification ()
	{
		// GenericCallbackImpl implements IGenericCallback<string>. The interface
		// is a TypeSpecification. The scanner must decode the blob to resolve it.
		var peers = ScanFixtures ();
		var impl = FindByJavaName (peers, "my/app/GenericCallbackImpl");
		Assert.Contains ("my/app/IGenericCallback", impl.ImplementedInterfaceJavaNames);
	}

	[Fact]
	public void Scan_ComponentOnlyBase_DerivedTypeIsDiscovered ()
	{
		// BaseActivityNoRegister has [Activity] but no [Register].
		// DerivedFromComponentBase extends it. ExtendsJavaPeerCore must
		// detect the component attribute on the base to include the derived type.
		var peers = ScanFixtures ();
		var derived = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.DerivedFromComponentBase");
		Assert.NotNull (derived);
		// Should get a CRC64-computed JNI name
		Assert.StartsWith ("crc64", derived.JavaName);
	}

	[Fact]
	public void Scan_ComponentOnlyBase_BaseTypeIsDiscovered ()
	{
		// BaseActivityNoRegister has [Activity(Name = "...")] — should be discovered
		// even without [Register].
		var peers = ScanFixtures ();
		var baseType = FindByJavaName (peers, "my/app/BaseActivityNoRegister");
		Assert.True (baseType.IsUnconditional, "[Activity] makes it unconditional");
		Assert.Equal ("android/app/Activity", baseType.BaseJavaName);
	}

	[Fact]
	public void Scan_UnregisteredNestedType_UsesParentJniPrefix ()
	{
		// UnregisteredChild has no [Register] but its parent RegisteredParent does.
		// ComputeTypeNameParts should use parent's JNI name as prefix.
		var peers = ScanFixtures ();
		var child = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.RegisteredParent+UnregisteredChild");
		Assert.NotNull (child);
		Assert.Equal ("my/app/RegisteredParent_UnregisteredChild", child.JavaName);
	}

	[Fact]
	public void Scan_EmptyNamespace_RegisteredType_Discovered ()
	{
		// GlobalType has [Register] and no namespace — should work normally.
		var peers = ScanFixtures ();
		var global = FindByJavaName (peers, "my/app/GlobalType");
		Assert.Equal ("GlobalType", global.ManagedTypeName);
	}

	[Fact]
	public void Scan_EmptyNamespace_UnregisteredType_CompatJniHasNoSlash ()
	{
		// GlobalUnregisteredType has no namespace and no [Register].
		// CompatJniName should just be the type name (no leading slash).
		var peers = ScanFixtures ();
		var global = peers.FirstOrDefault (p => p.ManagedTypeName == "GlobalUnregisteredType");
		Assert.NotNull (global);
		Assert.Equal ("GlobalUnregisteredType", global.CompatJniName);
		Assert.DoesNotContain ("/", global.CompatJniName);
	}

	[Fact]
	public void Scan_DeepNestedType_ThreeLevelNesting ()
	{
		// DeepOuter.Middle.DeepInner — 3-level nesting.
		// ComputeTypeNameParts walks multiple declaring-type levels.
		var peers = ScanFixtures ();
		var deep = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.DeepOuter+Middle+DeepInner");
		Assert.NotNull (deep);
		Assert.Equal ("my/app/DeepOuter_Middle_DeepInner", deep.JavaName);
	}

	[Fact]
	public void Scan_PlainActivitySubclass_DiscoveredWithCrc64Name ()
	{
		// PlainActivitySubclass extends Activity with no [Register], no [Activity].
		// ExtendsJavaPeer detects it via the base type chain, gets CRC64 name.
		var peers = ScanFixtures ();
		var plain = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.PlainActivitySubclass");
		Assert.NotNull (plain);
		Assert.StartsWith ("crc64", plain.JavaName);
		Assert.Equal ("android/app/Activity", plain.BaseJavaName);
	}

	[Fact]
	public void Scan_ComponentAttributeWithoutName_DiscoveredWithCrc64Name ()
	{
		// UnnamedActivity has [Activity(Label="Unnamed")] but no Name property.
		// HasComponentAttribute = true, ComponentAttributeJniName should be null,
		// and the type should still get a CRC64-based JNI name.
		var peers = ScanFixtures ();
		var unnamed = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.UnnamedActivity");
		Assert.NotNull (unnamed);
		Assert.StartsWith ("crc64", unnamed.JavaName);
		Assert.True (unnamed.IsUnconditional, "[Activity] makes it unconditional");
	}

	[Fact]
	public void Scan_InterfaceOnUnregisteredType_InterfacesResolved ()
	{
		// UnregisteredClickListener has no [Register] but implements IOnClickListener.
		// Type gets CRC64 name, interfaces still resolved.
		var peers = ScanFixtures ();
		var listener = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.UnregisteredClickListener");
		Assert.NotNull (listener);
		Assert.StartsWith ("crc64", listener.JavaName);
		Assert.Contains ("android/view/View$OnClickListener", listener.ImplementedInterfaceJavaNames);
	}

	[Fact]
	public void Scan_ExportOnUnregisteredType_MethodDiscovered ()
	{
		// UnregisteredExporter has [Export("doExportedWork")] on a type without [Register].
		// Type gets CRC64 name, [Export] method is in MarshalMethods.
		var peers = ScanFixtures ();
		var exporter = peers.FirstOrDefault (p => p.ManagedTypeName == "MyApp.UnregisteredExporter");
		Assert.NotNull (exporter);
		Assert.StartsWith ("crc64", exporter.JavaName);
		var exportMethod = exporter.MarshalMethods.FirstOrDefault (m => m.JniName == "doExportedWork");
		Assert.NotNull (exportMethod);
		Assert.Null (exportMethod.Connector);
	}

	[Fact]
	public void Scan_StaticExportMethod_IsStaticSetTrue ()
	{
		var peers = ScanFixtures ();
		var peer = FindByJavaName (peers, "my/app/ExportStaticAndFields");
		var staticMethod = peer.MarshalMethods.FirstOrDefault (m => m.JniName == "staticMethodNotMangled");
		Assert.NotNull (staticMethod);
		Assert.True (staticMethod.IsStatic);
		Assert.Null (staticMethod.Connector);
	}

	[Fact]
	public void Scan_InstanceExportMethod_IsStaticSetFalse ()
	{
		var peers = ScanFixtures ();
		var peer = FindByJavaName (peers, "my/app/ExportStaticAndFields");
		var instanceMethod = peer.MarshalMethods.FirstOrDefault (m => m.JniName == "instanceMethod");
		Assert.NotNull (instanceMethod);
		Assert.False (instanceMethod.IsStatic);
	}

	[Fact]
	public void Scan_ExportField_CollectedAsExportFieldInfo ()
	{
		var peers = ScanFixtures ();
		var peer = FindByJavaName (peers, "my/app/ExportStaticAndFields");

		Assert.NotNull (peer.ExportFields);
		Assert.Equal (2, peer.ExportFields.Count);

		var staticField = peer.ExportFields.FirstOrDefault (f => f.FieldName == "STATIC_INSTANCE");
		Assert.NotNull (staticField);
		Assert.Equal ("GetInstance", staticField.MethodName);
		Assert.True (staticField.IsStatic);

		var instanceField = peer.ExportFields.FirstOrDefault (f => f.FieldName == "VALUE");
		Assert.NotNull (instanceField);
		Assert.Equal ("GetValue", instanceField.MethodName);
		Assert.False (instanceField.IsStatic);
	}

	[Fact]
	public void Scan_ExportField_MethodAlsoInMarshalMethods ()
	{
		var peers = ScanFixtures ();
		var peer = FindByJavaName (peers, "my/app/ExportStaticAndFields");

		// [ExportField] methods should also be in MarshalMethods as export methods
		var getInstance = peer.MarshalMethods.FirstOrDefault (m => m.ManagedMethodName == "GetInstance");
		Assert.NotNull (getInstance);
		Assert.Null (getInstance.Connector); // Export, not Register
		Assert.True (getInstance.IsStatic);

		var getValue = peer.MarshalMethods.FirstOrDefault (m => m.ManagedMethodName == "GetValue");
		Assert.NotNull (getValue);
		Assert.Null (getValue.Connector);
		Assert.False (getValue.IsStatic);
	}
}
