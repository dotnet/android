using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
[Fact]
public void Scan_MarshalMethod_JniNameIsJavaMethodName ()
{
var peers = ScanFixtures ();
var onCreate = FindByJavaName (peers, "android/app/Activity")
.MarshalMethods.FirstOrDefault (m => m.ManagedMethodName == "OnCreate");
Assert.NotNull (onCreate);
Assert.Equal ("onCreate", onCreate.JniName);
}

[Fact]
public void Scan_UserTypeOverride_CollectsMarshalMethods ()
{
var peers = ScanFixtures ();
var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
var onCreate = mainActivity.MarshalMethods.FirstOrDefault (m => m.ManagedMethodName == "OnCreate");
Assert.NotNull (onCreate);
Assert.Equal ("(Landroid/os/Bundle;)V", onCreate.JniSignature);
}

[Fact]
public void Scan_TypeWithRegisteredCtors_HasConstructorMarshalMethods ()
{
var peers = ScanFixtures ();
var ctors = FindByJavaName (peers, "my/app/CustomView")
.MarshalMethods.Where (m => m.IsConstructor).ToList ();
Assert.Equal (2, ctors.Count);
Assert.Equal ("()V", ctors [0].JniSignature);
Assert.Equal ("(Landroid/content/Context;)V", ctors [1].JniSignature);
}

[Fact]
public void Scan_TypeWithoutRegisteredCtors_HasNoConstructorMarshalMethods ()
{
var peers = ScanFixtures ();
Assert.DoesNotContain (FindByJavaName (peers, "my/app/MyHelper").MarshalMethods, m => m.IsConstructor);
}

[Fact]
public void Scan_MarshalMethod_BoolReturn_HasCorrectJniSignature ()
{
var peers = ScanFixtures ();
var onTouch = FindByJavaName (peers, "my/app/TouchHandler")
.MarshalMethods.FirstOrDefault (m => m.JniName == "onTouch");
Assert.NotNull (onTouch);
Assert.Equal ("(Landroid/view/View;I)Z", onTouch.JniSignature);
}

[Fact]
public void Scan_MarshalMethod_BoolParam_HasCorrectJniSignature ()
{
var peers = ScanFixtures ();
var onFocus = FindByJavaName (peers, "my/app/TouchHandler")
.MarshalMethods.FirstOrDefault (m => m.JniName == "onFocusChange");
Assert.NotNull (onFocus);
Assert.Equal ("(Landroid/view/View;Z)V", onFocus.JniSignature);
}

[Fact]
public void Scan_MarshalMethod_MultiplePrimitiveParams ()
{
var peers = ScanFixtures ();
var onScroll = FindByJavaName (peers, "my/app/TouchHandler")
.MarshalMethods.FirstOrDefault (m => m.JniName == "onScroll");
Assert.NotNull (onScroll);
Assert.Equal ("(IFJD)V", onScroll.JniSignature);
}

[Fact]
public void Scan_MarshalMethod_ArrayParam ()
{
var peers = ScanFixtures ();
var setItems = FindByJavaName (peers, "my/app/TouchHandler")
.MarshalMethods.FirstOrDefault (m => m.JniName == "setItems");
Assert.NotNull (setItems);
Assert.Equal ("([Ljava/lang/String;)V", setItems.JniSignature);
}

[Fact]
public void Scan_ExportMethod_CollectedAsMarshalMethod ()
{
var peers = ScanFixtures ();
var method = FindByJavaName (peers, "my/app/ExportExample").MarshalMethods.Single ();
Assert.Equal ("myExportedMethod", method.JniName);
Assert.Null (method.Connector);
}

[Fact]
public void Scan_AbstractMethod_CollectedAsMarshalMethod ()
{
var peers = ScanFixtures ();
var doWork = FindByJavaName (peers, "my/app/AbstractBase")
.MarshalMethods.FirstOrDefault (m => m.JniName == "doWork");
Assert.NotNull (doWork);
Assert.Equal ("()V", doWork.JniSignature);
}

[Fact]
public void Scan_PropertyRegister_CollectedAsMarshalMethod ()
{
var peers = ScanFixtures ();
var getMessage = FindByJavaName (peers, "java/lang/Throwable")
.MarshalMethods.FirstOrDefault (m => m.JniName == "getMessage");
Assert.NotNull (getMessage);
Assert.Equal ("()Ljava/lang/String;", getMessage.JniSignature);
}

[Fact]
public void Scan_MethodWithEmptyConnector_Collected ()
{
var peers = ScanFixtures ();
var onStart = FindByJavaName (peers, "android/app/Activity")
.MarshalMethods.FirstOrDefault (m => m.JniName == "onStart");
Assert.NotNull (onStart);
Assert.Equal ("", onStart.Connector);
}

[Fact]
public void Scan_InterfaceMethod_CollectedAsMarshalMethod ()
{
var peers = ScanFixtures ();
var onClick = FindByManagedName (peers, "Android.Views.IOnClickListener")
.MarshalMethods.FirstOrDefault (m => m.JniName == "onClick");
Assert.NotNull (onClick);
Assert.Equal ("(Landroid/view/View;)V", onClick.JniSignature);
}

[Fact]
public void Scan_Interface_HasInvokerTypeName ()
{
var peers = ScanFixtures ();
var listener = FindByManagedName (peers, "Android.Views.IOnClickListener");
Assert.Equal ("Android.Views.IOnClickListenerInvoker", listener.InvokerTypeName);
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
}

[Theory]
[InlineData ("java/lang/Object", null)]
[InlineData ("android/app/Activity", "java/lang/Object")]
[InlineData ("my/app/MainActivity", "android/app/Activity")]
[InlineData ("java/lang/Throwable", "java/lang/Object")]
[InlineData ("java/lang/Exception", "java/lang/Throwable")]
[InlineData ("my/app/MyButton", "android/widget/Button")]
public void Scan_BaseJavaName_ResolvesCorrectly (string javaName, string? expectedBase)
{
var peers = ScanFixtures ();
Assert.Equal (expectedBase, FindByJavaName (peers, javaName).BaseJavaName);
}

[Fact]
public void Scan_TypeImplementingInterface_HasInterfaceJavaNames ()
{
var peers = ScanFixtures ();
Assert.Contains ("android/view/View$OnClickListener",
FindByJavaName (peers, "my/app/ClickableView").ImplementedInterfaceJavaNames);
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
public void Scan_TypeNotImplementingInterface_HasEmptyList ()
{
var peers = ScanFixtures ();
Assert.Empty (FindByJavaName (peers, "my/app/MyHelper").ImplementedInterfaceJavaNames);
}

[Fact]
public void Scan_DeepHierarchy_InheritsActivationCtor ()
{
var peers = ScanFixtures ();
var myButton = FindByJavaName (peers, "my/app/MyButton");
Assert.NotNull (myButton.ActivationCtor);
Assert.Equal (ActivationCtorStyle.XamarinAndroid, myButton.ActivationCtor.Style);
}

[Fact]
public void Scan_BackupAgent_ForcedUnconditional ()
{
var peers = ScanFixtures ();
Assert.True (FindByJavaName (peers, "my/app/MyBackupAgent").IsUnconditional);
}

[Fact]
public void Scan_ManageSpaceActivity_ForcedUnconditional ()
{
var peers = ScanFixtures ();
Assert.True (FindByJavaName (peers, "my/app/MyManageSpaceActivity").IsUnconditional);
}

[Fact]
public void Scan_CompatJniName_RegisteredType_EqualsJavaName ()
{
var peers = ScanFixtures ();
var activity = FindByJavaName (peers, "android/app/Activity");
Assert.Equal (activity.JavaName, activity.CompatJniName);
var mainActivity = FindByJavaName (peers, "my/app/MainActivity");
Assert.Equal (mainActivity.JavaName, mainActivity.CompatJniName);
}

[Fact]
public void Scan_CompatJniName_UnregisteredType_UsesRawNamespace ()
{
var peers = ScanFixtures ();
var unregistered = FindByManagedName (peers, "MyApp.UnregisteredHelper");
Assert.StartsWith ("crc64", unregistered.JavaName);
Assert.Equal ("myapp/UnregisteredHelper", unregistered.CompatJniName);
}

[Fact]
public void Scan_CustomJniNameProviderAttribute_UsesNameFromAttribute ()
{
var peers = ScanFixtures ();
var widget = FindByManagedName (peers, "MyApp.CustomWidget");
Assert.Equal ("com/example/CustomWidget", widget.JavaName);
}

[Fact]
public void Scan_NestedType_IsDiscovered ()
{
var peers = ScanFixtures ();
Assert.Equal ("MyApp.Outer+Inner", FindByJavaName (peers, "my/app/Outer$Inner").ManagedTypeName);
}

[Fact]
public void Scan_NestedTypeInInterface_IsDiscovered ()
{
var peers = ScanFixtures ();
Assert.Equal ("MyApp.ICallback+Result", FindByJavaName (peers, "my/app/ICallback$Result").ManagedTypeName);
}
}
