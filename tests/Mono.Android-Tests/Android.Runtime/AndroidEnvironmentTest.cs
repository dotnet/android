using System;
using System.Reflection;
using Android.Runtime;
using NUnit.Framework;

namespace Android.RuntimeTests {
	[TestFixture]
	public class AndroidEnvironmentTest {
		const string EnvironmentVariable = "XA_HTTP_CLIENT_HANDLER_TYPE";

		static object envLock = new object ();

		private string? _originalValue = null;

		[TestFixtureSetUp]
		public void SetUp ()
		{
			_originalValue = Environment.GetEnvironmentVariable (EnvironmentVariable);
		}

		[TestFixtureTearDown]
		public void TearDown ()
		{
			Environment.SetEnvironmentVariable (EnvironmentVariable, _originalValue);
		}

		[Test]
		[TestCase (null)]
		[TestCase ("Xamarin.Android.Net.AndroidHttpResponseMessage")] // does not extend HttpMessageHandler
#if NET
		[TestCase ("Xamarin.Android.Net.AndroidClientHandler")] // instantiating AndroidClientHandler would cause infinite recursion
#endif
		public void GetHttpMessageHandler_FallbackToAndroidMessageHandler (string? typeName)
		{
			var handler = GetHttpMessageHandler (typeName);

			Assert.IsNotNull (handler, "GetHttpMessageHandler returned null");
			Assert.IsNotNull ("Xamarin.Android.Net.AndroidMessageHandler", handler.GetType ().FullName);
		}

		[Test]
		[TestCase ("System.Net.Http.HttpClientHandler")] // the type name doesn't contain the name of the assembly so the type won't be found
		[TestCase ("Some.Nonexistent.Type")]
		public void GetHttpMessageHandler_FallbackForInaccessibleTypes (string typeName)
		{
			var handler = GetHttpMessageHandler (typeName);

			Assert.IsNotNull (handler, "GetHttpMessageHandler returned null");
			Assert.IsNotNull ("Xamarin.Android.Net.AndroidMessageHandler", handler.GetType ().FullName);
		}

		[Test]
		[TestCase ("Xamarin.Android.Net.AndroidMessageHandler")]
#if !NET
		[TestCase ("Xamarin.Android.Net.AndroidClientHandler")]
#endif
		public void GetHttpMessageHandler_OverridesDefaultValue (string typeName)
		{
			var handler = GetHttpMessageHandler (typeName);

			Assert.IsNotNull (handler, "GetHttpMessageHandler returned null");
			Assert.AreEqual (typeName, handler.GetType ().FullName);
		}

		private static object? GetHttpMessageHandler (string? typeName)
		{
			var method = typeof (AndroidEnvironment).GetMethod ("GetHttpMessageHandler", BindingFlags.Static | BindingFlags.NonPublic);
			lock (envLock) {
				Environment.SetEnvironmentVariable (EnvironmentVariable, typeName);
				return method.Invoke (null, null);
			}
		}
	}
}
