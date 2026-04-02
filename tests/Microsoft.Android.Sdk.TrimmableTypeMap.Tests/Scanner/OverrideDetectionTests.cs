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
		Assert.Equal ("n_OnCreate_Landroid_os_Bundle_", onCreate.NativeCallbackName);
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

	[Fact]
	public void DerivedFragment_OnViewCreated_DeclaringTypePointsToBaseFragment ()
	{
		// DerivedFragment overrides BaseFragment.OnViewCreated (ACW→ACW chain)
		var peer = FindFixtureByJavaName ("my/app/DerivedFragment");
		var onViewCreated = Assert.Single (peer.MarshalMethods, m => m.JniName == "onViewCreated");
		Assert.Equal ("MyApp.BaseFragment", onViewCreated.DeclaringTypeName);
		Assert.Equal ("TestFixtures", onViewCreated.DeclaringAssemblyName);
	}

	[Fact]
	public void AbstractBaseMethod_OverrideDetected ()
	{
		// ConcreteImpl overrides AbstractBase.DoWork which is abstract + [Register]
		var peer = FindFixtureByJavaName ("my/app/ConcreteImpl");
		var doWork = Assert.Single (peer.MarshalMethods, m => m.JniName == "doWork");
		Assert.Equal ("()V", doWork.JniSignature);
	}

	[Fact]
	public void MultipleOverloads_PicksCorrectOne ()
	{
		// OverloadDerived overrides Process(int) but NOT Process()
		var peer = FindFixtureByJavaName ("my/app/OverloadDerived");
		var nonCtorMethods = peer.MarshalMethods.Where (m => !m.IsConstructor).ToList ();
		var processInt = Assert.Single (nonCtorMethods, m => m.JniName == "process" && m.JniSignature == "(I)V");
		Assert.Equal ("GetProcess_IHandler", processInt.Connector);
		// Process() should NOT be detected (not overridden)
		Assert.DoesNotContain (nonCtorMethods, m => m.JniName == "process" && m.JniSignature == "()V");
	}

	[Fact]
	public void EmptyConnector_OverrideStillDetected ()
	{
		// Activity.OnStart has [Register("onStart", "()V", "")] — empty connector
		// FullActivity overrides it without [Register]
		var peer = FindFixtureByJavaName ("my/app/FullActivity");
		var onStart = Assert.Single (peer.MarshalMethods, m => m.JniName == "onStart");
		Assert.Equal ("", onStart.Connector);
	}
}
