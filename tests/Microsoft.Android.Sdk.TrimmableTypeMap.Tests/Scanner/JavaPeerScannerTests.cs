using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
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
}
