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
	public void Override_DetectedWithCorrectRegistration ()
	{
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		var onCreate = peer.MarshalMethods.First (m => m.JniName == "onCreate");
		Assert.Equal ("(Landroid/os/Bundle;)V", onCreate.JniSignature);
		Assert.Equal ("n_OnCreate", onCreate.NativeCallbackName);
		Assert.False (onCreate.IsConstructor);
		Assert.Equal ("GetOnCreate_Landroid_os_Bundle_Handler", onCreate.Connector);
		Assert.NotNull (peer.ActivationCtor);
	}

	[Fact]
	public void MultipleOverrides_AllDetected ()
	{
		var peer = FindFixtureByJavaName ("my/app/FullActivity");
		var nonCtorMarshalNames = peer.MarshalMethods.Where (m => !m.IsConstructor).Select (m => m.JniName).ToList ();
		Assert.Equal (2, nonCtorMarshalNames.Count);
		Assert.Contains ("onCreate", nonCtorMarshalNames);
		Assert.Contains ("onStart", nonCtorMarshalNames);
	}

	[Fact]
	public void DeepInheritance_OverrideDetectedAcrossMultipleLevels ()
	{
		var peer = FindFixtureByJavaName ("my/app/DeeplyDerived");
		Assert.Contains (peer.MarshalMethods, m => m.JniName == "onCreate");
	}

	[Fact]
	public void MixedMethods_DirectRegisterAndOverrideBothPresentNoDuplicates ()
	{
		var peer = FindFixtureByJavaName ("my/app/MixedMethods");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onCreate", marshalNames);
		Assert.Contains ("customMethod", marshalNames);
		var marshalKeys = peer.MarshalMethods.Select (m => $"{m.JniName}:{m.JniSignature}").ToList ();
		Assert.Equal (marshalKeys.Count, marshalKeys.Distinct ().Count ());
	}

	[Fact]
	public void NewSlot_NotDetectedAsOverride ()
	{
		var peer = FindFixtureByJavaName ("my/app/NewSlotActivity");
		Assert.DoesNotContain (peer.MarshalMethods, m => m.JniName == "onCreate");
	}

	[Fact]
	public void PropertyOverride_DetectedWithCorrectSignature ()
	{
		var peer = FindFixtureByJavaName ("my/app/CustomException");
		var getMessage = peer.MarshalMethods.First (m => m.JniName == "getMessage");
		Assert.Equal ("()Ljava/lang/String;", getMessage.JniSignature);
	}

	[Fact]
	public void DirectRegister_StillWorksForMainActivity ()
	{
		var peer = FindFixtureByJavaName ("my/app/MainActivity");
		Assert.Contains (peer.MarshalMethods, m => m.JniName == "onCreate");
	}
}
