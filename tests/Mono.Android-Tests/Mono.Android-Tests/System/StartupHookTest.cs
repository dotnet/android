using System;
using System.Reflection;
using NUnit.Framework;

namespace SystemTests
{
	[TestFixture]
	public class StartupHookTest
	{
		[Test]
		public void FeatureFlagIsEnabled ()
		{
			// NOTE: this is set to true in tests\Mono.Android-Tests\Mono.Android-Tests\Mono.Android.NET-Tests.csproj
			Assert.IsTrue (Microsoft.Android.Runtime.RuntimeFeature.StartupHookSupport, "RuntimeFeature.StartupHookSupport should be true");
		}

		[Test]
		public void EnvironmentVariableIsSet ()
		{
			var value = Environment.GetEnvironmentVariable ("DOTNET_STARTUP_HOOKS");
			Assert.AreEqual ("StartupHook", value, "DOTNET_STARTUP_HOOKS should be set to 'StartupHook'");
		}

		[Test]
		public void IsInitialized ()
		{
			var type = Type.GetType ("StartupHook, StartupHook", throwOnError: true);
			Assert.IsNotNull (type, "StartupHook type should be loaded");

			var property = type.GetProperty ("IsInitialized", BindingFlags.Public | BindingFlags.Static);
			Assert.IsNotNull (property, "IsInitialized property should exist");

			var value = (bool) property.GetValue (null);
			Assert.IsTrue (value, "StartupHook.Initialize() should have been called");
		}
	}
}
