using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

public partial class JavaPeerScannerTests
{
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
