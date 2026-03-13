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
	public void UserActivity_OverrideDetectedWithCorrectRegistration ()
	{
		// UserActivity overrides Activity.OnCreate without [Register] on the override
		var peer = FindFixtureByJavaName ("my/app/UserActivity");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onCreate", marshalNames);

		var onCreate = peer.MarshalMethods.First (m => m.JniName == "onCreate");
		Assert.Equal ("(Landroid/os/Bundle;)V", onCreate.JniSignature);
		Assert.Equal ("n_OnCreate", onCreate.NativeCallbackName);
		Assert.False (onCreate.IsConstructor);
		// Activity.OnCreate has connector "GetOnCreate_Landroid_os_Bundle_Handler"
		Assert.Equal ("GetOnCreate_Landroid_os_Bundle_Handler", onCreate.Connector);
		// The n_OnCreate callback lives on Activity, not UserActivity
		Assert.Equal ("Android.App.Activity", onCreate.DeclaringTypeName);
		Assert.Equal ("TestFixtures", onCreate.DeclaringAssemblyName);

		// UserActivity has an activation ctor
		Assert.NotNull (peer.ActivationCtor);
	}

	[Fact]
	public void MultipleOverrides_AllDetected ()
	{
		// FullActivity overrides both OnCreate and OnStart — and nothing else.
		// Non-registered Object virtuals (ToString, Equals, GetHashCode) must NOT appear.
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
		var onCreate = Assert.Single (peer.MarshalMethods, m => m.JniName == "onCreate");
		// DeclaringType must be Activity (where [Register] lives), not UserActivity
		Assert.Equal ("Android.App.Activity", onCreate.DeclaringTypeName);
		Assert.Equal ("TestFixtures", onCreate.DeclaringAssemblyName);
	}

	[Fact]
	public void MixedMethods_DirectRegisterAndOverrideBothPresent ()
	{
		// MixedMethods has direct [Register("customMethod")] AND overrides OnCreate (no [Register])
		var peer = FindFixtureByJavaName ("my/app/MixedMethods");
		var marshalNames = peer.MarshalMethods.Select (m => m.JniName).ToList ();
		Assert.Contains ("onCreate", marshalNames);
		Assert.Contains ("customMethod", marshalNames);

		// No duplicate entries
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

		var getMessage = peer.MarshalMethods.First (m => m.JniName == "getMessage");
		Assert.Equal ("()Ljava/lang/String;", getMessage.JniSignature);
		// The n_get_Message callback lives on Throwable, not CustomException
		Assert.Equal ("Java.Lang.Throwable", getMessage.DeclaringTypeName);
		Assert.Equal ("TestFixtures", getMessage.DeclaringAssemblyName);
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
	public void DerivedFragment_DeclaringTypePointsToCorrectBase ()
	{
		// DerivedFragment overrides Activity.OnCreate (MCW, 2 levels up) and
		// BaseFragment.OnViewCreated (user ACW, 1 level up). Each override
		// must point to the type that owns the [Register].
		var peer = FindFixtureByJavaName ("my/app/DerivedFragment");
		var nonCtorMethods = peer.MarshalMethods.Where (m => !m.IsConstructor).ToList ();

		var onCreate = Assert.Single (nonCtorMethods, m => m.JniName == "onCreate");
		Assert.Equal ("Android.App.Activity", onCreate.DeclaringTypeName);

		var onViewCreated = Assert.Single (nonCtorMethods, m => m.JniName == "onViewCreated");
		Assert.Equal ("MyApp.BaseFragment", onViewCreated.DeclaringTypeName);
		Assert.Equal ("TestFixtures", onViewCreated.DeclaringAssemblyName);
	}

	[Fact]
	public void GrandchildFragment_ThreeLevelDeepOverride ()
	{
		// GrandchildFragment → DerivedFragment → BaseFragment → Activity
		// OnCreate [Register] is on Activity, 3 levels up
		var peer = FindFixtureByJavaName ("my/app/GrandchildFragment");
		var onCreate = Assert.Single (peer.MarshalMethods, m => m.JniName == "onCreate");
		Assert.Equal ("Android.App.Activity", onCreate.DeclaringTypeName);
	}
}
