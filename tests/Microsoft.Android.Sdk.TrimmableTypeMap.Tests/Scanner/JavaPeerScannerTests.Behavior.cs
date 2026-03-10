using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
	[Theory]
	[InlineData ("android/app/Activity", "OnCreate", "onCreate", "(Landroid/os/Bundle;)V")]
	[InlineData ("android/app/Activity", "OnStart", "onStart", "()V")]
	[InlineData ("my/app/MainActivity", "OnCreate", "onCreate", "(Landroid/os/Bundle;)V")]
	[InlineData ("my/app/AbstractBase", "DoWork", "doWork", "()V")]
	[InlineData ("java/lang/Throwable", "Message", "getMessage", "()Ljava/lang/String;")]
	[InlineData ("my/app/TouchHandler", "OnTouch", "onTouch", "(Landroid/view/View;I)Z")]
	[InlineData ("my/app/TouchHandler", "OnFocusChange", "onFocusChange", "(Landroid/view/View;Z)V")]
	[InlineData ("my/app/TouchHandler", "OnScroll", "onScroll", "(IFJD)V")]
	[InlineData ("my/app/TouchHandler", "SetItems", "setItems", "([Ljava/lang/String;)V")]
	public void Scan_MarshalMethod_HasCorrectSignature (string javaName, string managedName, string jniName, string jniSig)
	{
		var peers = ScanFixtures ();
		var method = FindByJavaName (peers, javaName)
			.MarshalMethods.FirstOrDefault (m => m.ManagedMethodName == managedName || m.JniName == jniName);
		Assert.NotNull (method);
		Assert.Equal (jniName, method.JniName);
		Assert.Equal (jniSig, method.JniSignature);
	}

	[Fact]
	public void Scan_MarshalMethod_ConstructorsAndSpecialCases ()
	{
		var peers = ScanFixtures ();

		var ctors = FindByJavaName (peers, "my/app/CustomView")
			.MarshalMethods.Where (m => m.IsConstructor).ToList ();
		Assert.Equal (2, ctors.Count);
		Assert.Equal ("()V", ctors [0].JniSignature);
		Assert.Equal ("(Landroid/content/Context;)V", ctors [1].JniSignature);

		Assert.DoesNotContain (FindByJavaName (peers, "my/app/MyHelper").MarshalMethods, m => m.IsConstructor);

		var exportMethod = FindByJavaName (peers, "my/app/ExportExample").MarshalMethods.Single ();
		Assert.Equal ("myExportedMethod", exportMethod.JniName);
		Assert.Null (exportMethod.Connector);

		var onStart = FindByJavaName (peers, "android/app/Activity")
			.MarshalMethods.FirstOrDefault (m => m.JniName == "onStart");
		Assert.NotNull (onStart);
		Assert.Equal ("", onStart.Connector);

		var onClick = FindByManagedName (peers, "Android.Views.IOnClickListener")
			.MarshalMethods.FirstOrDefault (m => m.JniName == "onClick");
		Assert.NotNull (onClick);
		Assert.Equal ("(Landroid/view/View;)V", onClick.JniSignature);

		Assert.Equal ("Android.Views.IOnClickListenerInvoker",
			FindByManagedName (peers, "Android.Views.IOnClickListener").InvokerTypeName);
	}

	[Theory]
	[InlineData ("android/app/Activity", "Android.App.Activity")]
	[InlineData ("my/app/SimpleActivity", "Android.App.Activity")]
	[InlineData ("my/app/MyButton", "MyApp.MyButton")]
	public void Scan_ActivationCtor_InheritsFromNearestBase (string javaName, string expectedDeclaringType)
	{
		var peers = ScanFixtures ();
		var peer = FindByJavaName (peers, javaName);
		Assert.NotNull (peer.ActivationCtor);
		Assert.Equal (expectedDeclaringType, peer.ActivationCtor.DeclaringTypeName);
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
	public void Scan_MultipleInterfaces_AllResolved ()
	{
		var peers = ScanFixtures ();

		var multi = FindByJavaName (peers, "my/app/MultiInterfaceView");
		Assert.Contains ("android/view/View$OnClickListener", multi.ImplementedInterfaceJavaNames);
		Assert.Contains ("android/view/View$OnLongClickListener", multi.ImplementedInterfaceJavaNames);
		Assert.Equal (2, multi.ImplementedInterfaceJavaNames.Count);

		Assert.Contains ("android/view/View$OnClickListener",
			FindByJavaName (peers, "my/app/ClickableView").ImplementedInterfaceJavaNames);
		Assert.Empty (FindByJavaName (peers, "my/app/MyHelper").ImplementedInterfaceJavaNames);
	}

	[Theory]
	[InlineData ("android/app/Activity", "android/app/Activity")]
	[InlineData ("my/app/MainActivity", "my/app/MainActivity")]
	public void Scan_CompatJniName (string javaName, string expectedCompat)
	{
		var peers = ScanFixtures ();
		Assert.Equal (expectedCompat, FindByJavaName (peers, javaName).CompatJniName);
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
		Assert.Equal ("com/example/CustomWidget",
			FindByManagedName (peers, "MyApp.CustomWidget").JavaName);
	}

	[Theory]
	[InlineData ("my/app/Outer$Inner", "MyApp.Outer+Inner")]
	[InlineData ("my/app/ICallback$Result", "MyApp.ICallback+Result")]
	public void Scan_NestedType_IsDiscovered (string javaName, string managedName)
	{
		var peers = ScanFixtures ();
		Assert.Equal (managedName, FindByJavaName (peers, javaName).ManagedTypeName);
	}
}
