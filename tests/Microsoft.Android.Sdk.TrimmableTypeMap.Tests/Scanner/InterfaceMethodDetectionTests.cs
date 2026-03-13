using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for interface method detection: the scanner must find marshal methods
/// on types that implement Java interfaces even when the implementing method
/// has no [Register] attribute. The legacy pipeline handles this via the
/// interface loop in CecilImporter.cs lines 100-120.
/// </summary>
public class InterfaceMethodDetectionTests : FixtureTestBase
{
	[Fact]
	public void ImplicitInterfaceImpl_OnClick_IsDetected ()
	{
		var peer = FindFixtureByJavaName ("my/app/ImplicitClickListener");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onClick", marshalNames);
	}

	[Fact]
	public void ImplicitInterfaceImpl_HasCorrectJniSignature ()
	{
		var peer = FindFixtureByJavaName ("my/app/ImplicitClickListener");
		var onClick = peer.MarshalMethods.First (m => m.JniName == "onClick");
		Assert.Equal ("(Landroid/view/View;)V", onClick.JniSignature);
	}

	[Fact]
	public void ImplicitInterfaceImpl_HasCorrectConnector ()
	{
		var peer = FindFixtureByJavaName ("my/app/ImplicitClickListener");
		var onClick = peer.MarshalMethods.First (m => m.JniName == "onClick");
		Assert.Equal ("GetOnClick_Landroid_view_View_Handler:Android.Views.IOnClickListenerInvoker", onClick.Connector);
	}

	[Fact]
	public void ImplicitMultiInterface_BothMethodsDetected ()
	{
		var peer = FindFixtureByJavaName ("my/app/ImplicitMultiListener");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onClick", marshalNames);
		Assert.Contains ("onLongClick", marshalNames);
	}

	[Fact]
	public void MixedInterfaceImpl_DirectAndImplicitBothPresent ()
	{
		// OnClick has [Register] directly, OnLongClick is implicit from interface
		var peer = FindFixtureByJavaName ("my/app/MixedInterfaceImpl");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onClick", marshalNames);
		Assert.Contains ("onLongClick", marshalNames);
	}

	[Fact]
	public void MixedInterfaceImpl_NoDuplicates ()
	{
		var peer = FindFixtureByJavaName ("my/app/MixedInterfaceImpl");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Equal (marshalNames.Count, marshalNames.Distinct ().Count ());
	}

	[Fact]
	public void ExplicitRegister_StillWorks ()
	{
		// ClickableView has [Register("onClick",...)] directly — should still work
		var peer = FindFixtureByJavaName ("my/app/ClickableView");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onClick", marshalNames);
	}
}
