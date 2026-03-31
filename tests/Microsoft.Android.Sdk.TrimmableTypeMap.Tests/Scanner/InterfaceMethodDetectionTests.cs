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
	public void ImplicitInterfaceImpl_DetectsOnClickWithCorrectSignatureAndConnector ()
	{
		var peer = FindFixtureByJavaName ("my/app/ImplicitClickListener");
		var onClick = peer.MarshalMethods.First (m => m.JniName == "onClick");
		Assert.Equal ("(Landroid/view/View;)V", onClick.JniSignature);
		Assert.Equal ("GetOnClick_Landroid_view_View_Handler:Android.Views.IOnClickListenerInvoker", onClick.Connector);
		Assert.Equal ("Android.Views.IOnClickListenerInvoker", onClick.DeclaringTypeName);
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
	public void MixedInterfaceImpl_DirectAndImplicitBothPresentWithNoDuplicates ()
	{
		var peer = FindFixtureByJavaName ("my/app/MixedInterfaceImpl");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onClick", marshalNames);
		Assert.Contains ("onLongClick", marshalNames);
		Assert.Equal (marshalNames.Count, marshalNames.Distinct ().Count ());
	}

	[Fact]
	public void ExplicitRegister_StillWorks ()
	{
		var peer = FindFixtureByJavaName ("my/app/ClickableView");
		Assert.Contains (peer.MarshalMethods, m => m.JniName == "onClick");
	}

	[Fact]
	public void InterfacePropertyImpl_DetectedWithCorrectSignature ()
	{
		// Gap #1: ImplicitPropertyImpl implements IHasName.Name without [Register]
		var peer = FindFixtureByJavaName ("my/app/ImplicitPropertyImpl");
		var getName = peer.MarshalMethods.First (m => m.JniName == "getName");
		Assert.Equal ("()Ljava/lang/String;", getName.JniSignature);
		Assert.Equal ("GetGetNameHandler:Android.Views.IHasNameInvoker, TestFixtures, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null", getName.Connector);
		Assert.Equal ("Android.Views.IHasNameInvoker", getName.DeclaringTypeName);
		Assert.Equal ("TestFixtures", getName.DeclaringAssemblyName);
	}

	[Fact]
	public void InterfaceType_DoesNotGetInterfaceMethodDetection ()
	{
		// Gap #5: interfaces themselves should not have interface method detection applied
		// IOnClickListener has [Register] on onClick — it should NOT also pick up
		// methods from other interfaces it might extend
		var peer = FindFixtureByManagedName ("Android.Views.IOnClickListener");
		// Should only have the directly-registered onClick, nothing extra
		Assert.Single (peer.MarshalMethods);
		Assert.Equal ("onClick", peer.MarshalMethods [0].JniName);
	}

	[Fact]
	public void NonJavaPeerInterface_IsIgnored ()
	{
		// Gap #4: interfaces without [Register] should be skipped entirely
		// ImplicitClickListener implements IOnClickListener (Java peer) — should be found
		// If it also implemented a non-Java interface, those methods should NOT appear
		var peer = FindFixtureByJavaName ("my/app/ImplicitClickListener");
		// Only onClick from IOnClickListener, nothing from System.IDisposable etc.
		var nonCtorMethods = peer.MarshalMethods.Where (m => !m.IsConstructor).ToList ();
		Assert.Single (nonCtorMethods);
	}

	[Fact]
	public void InterfaceExtendsInterface_ParentMethodsDetected ()
	{
		// NamedClickListenerImpl implements INamedClickListener which extends IOnClickListener.
		// Should get both getLabel (from child) and onClick (from parent).
		var peer = FindFixtureByJavaName ("my/app/NamedClickListenerImpl");
		var nonCtorMethods = peer.MarshalMethods.Where (m => !m.IsConstructor).ToList ();
		Assert.Contains (nonCtorMethods, m => m.JniName == "getLabel");
		Assert.Contains (nonCtorMethods, m => m.JniName == "onClick");
	}
}
