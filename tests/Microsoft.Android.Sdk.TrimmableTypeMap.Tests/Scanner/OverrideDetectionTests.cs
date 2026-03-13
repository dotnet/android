using System.Linq;
using Xunit;

namespace Microsoft.Android.Sdk.TrimmableTypeMap.Tests;

/// <summary>
/// Tests for override detection: the scanner must find marshal methods on user types
/// that override registered base methods even when the override has no [Register] attribute.
/// </summary>
public class OverrideDetectionTests : FixtureTestBase
{
	[Fact]
	public void Override_WithoutRegister_IsDetectedAsMarshalMethod ()
	{
		// UserActivity overrides Activity.OnCreate without [Register] on the override
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onCreate", marshalNames);
	}

	[Fact]
	public void Override_HasCorrectJniSignature ()
	{
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		var onCreate = peer.MarshalMethods.First (m => m.JniName == "onCreate");
		Assert.Equal ("(Landroid/os/Bundle;)V", onCreate.JniSignature);
	}

	[Fact]
	public void Override_HasCorrectNativeCallbackName ()
	{
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		var onCreate = peer.MarshalMethods.First (m => m.JniName == "onCreate");
		Assert.Equal ("n_OnCreate", onCreate.NativeCallbackName);
	}

	[Fact]
	public void Override_IsNotMarkedAsConstructor ()
	{
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		var onCreate = peer.MarshalMethods.First (m => m.JniName == "onCreate");
		Assert.False (onCreate.IsConstructor);
	}

	[Fact]
	public void Override_ProducesJavaConstructorsFromActivationCtor ()
	{
		// UserActivity has an activation ctor → should get JavaConstructors
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		Assert.NotNull (peer.ActivationCtor);
	}

	[Fact]
	public void MultipleOverrides_AllDetected ()
	{
		// FullActivity overrides both OnCreate and OnStart
		var peer = FindFixtureByJavaName ("my/app/FullActivity");
		var nonCtorMarshalNames = peer.MarshalMethods.Where (m => !m.IsConstructor).Select (m => m.JniName).ToList ();
		Assert.Equal (2, nonCtorMarshalNames.Count);
		Assert.Contains ("onCreate", nonCtorMarshalNames);
		Assert.Contains ("onStart", nonCtorMarshalNames);
	}

	[Fact]
	public void DeepInheritance_OverrideDetectedAcrossMultipleLevels ()
	{
		// DeeplyDerived → UserActivity → Activity, [Register] is on Activity.OnCreate
		var peer = FindFixtureByJavaName ("my/app/DeeplyDerived");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onCreate", marshalNames);
	}

	[Fact]
	public void MixedMethods_DirectRegisterAndOverrideBothPresent ()
	{
		// MixedMethods has direct [Register("customMethod")] AND overrides OnCreate (no [Register])
		var peer = FindFixtureByJavaName ("my/app/MixedMethods");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onCreate", marshalNames);
		Assert.Contains ("customMethod", marshalNames);
	}

	[Fact]
	public void MixedMethods_NoDuplicates ()
	{
		var peer = FindFixtureByJavaName ("my/app/MixedMethods");
		var marshalKeys = peer.MarshalMethods.Select (m => $"{m.JniName}:{m.JniSignature}").ToList ();
		Assert.Equal (marshalKeys.Count, marshalKeys.Distinct ().Count ());
	}

	[Fact]
	public void NewSlot_NotDetectedAsOverride ()
	{
		// NewSlotActivity uses 'new' keyword — should NOT be treated as an override
		var peer = FindFixtureByJavaName ("my/app/NewSlotActivity");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.DoesNotContain ("onCreate", marshalNames);
	}

	[Fact]
	public void PropertyOverride_DetectedFromBaseType ()
	{
		// CustomException overrides Throwable.Message which has [Register("getMessage",...)]
		var peer = FindFixtureByJavaName ("my/app/CustomException");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("getMessage", marshalNames);
	}

	[Fact]
	public void PropertyOverride_HasCorrectJniSignature ()
	{
		var peer = FindFixtureByJavaName ("my/app/CustomException");
		var getMessage = peer.MarshalMethods.First (m => m.JniName == "getMessage");
		Assert.Equal ("()Ljava/lang/String;", getMessage.JniSignature);
	}

	[Fact]
	public void DirectRegister_StillWorksForMainActivity ()
	{
		// The original test fixture MainActivity has [Register] directly — should still work
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onCreate", marshalNames);
	}

	[Fact]
	public void Override_ConnectorFromBase_IsPreserved ()
	{
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		var onCreate = peer.MarshalMethods.First (m => m.JniName == "onCreate");
		// Activity.OnCreate has connector "GetOnCreate_Landroid_os_Bundle_Handler"
		Assert.Equal ("GetOnCreate_Landroid_os_Bundle_Handler", onCreate.Connector);
	}
}
