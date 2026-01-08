using System;
using System.Reflection;
using NUnit.Framework;

namespace SystemTests
{
	[TestFixture]
	public class StartupHookTest
	{
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
