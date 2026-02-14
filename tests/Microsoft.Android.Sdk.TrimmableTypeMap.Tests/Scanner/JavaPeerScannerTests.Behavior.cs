using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
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

}
