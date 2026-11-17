using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SystemTests
{
	[TestClass]
	public class StartupHookTest
	{
		[TestMethod]
		public void FeatureFlagIsEnabled ()
		{
			// NOTE: this is set to true in tests\Mono.Android-Tests\Mono.Android-Tests\Mono.Android.NET-Tests.csproj
			Assert.IsTrue (Microsoft.Android.Runtime.RuntimeFeature.StartupHookSupport, "RuntimeFeature.StartupHookSupport should be true");
		}

		[TestMethod]
		public void EnvironmentVariableIsSet ()
		{
			var value = Environment.GetEnvironmentVariable ("DOTNET_STARTUP_HOOKS");
			Assert.AreEqual ("StartupHook", value, "DOTNET_STARTUP_HOOKS should be set to 'StartupHook'");
		}

		[TestMethod]
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
