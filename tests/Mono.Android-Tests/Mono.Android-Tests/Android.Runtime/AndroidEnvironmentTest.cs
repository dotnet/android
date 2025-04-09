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
			ClearHttpMessageHandlerTypeCache ();
		}

		[Test]
		[TestCase (null)]
		[TestCase ("Xamarin.Android.Net.AndroidHttpResponseMessage")]
		[TestCase ("Xamarin.Android.Net.AndroidClientHandler")]
		[TestCase ("System.Net.Http.HttpClientHandler, System.Net.Http")]
		[TestCase ("System.Net.Http.HttpClientHandler")]
		[TestCase ("Some.Nonexistent.Type")]
		[TestCase ("System.Net.Http.SocketsHttpHandler, System.Net.Http")]
		public void GetHttpMessageHandler_IgnoresValueOfEnvironmentVariable (string? typeName)
		{
			var handler = GetHttpMessageHandler (typeName);

			Assert.IsNotNull (handler, "GetHttpMessageHandler returned null");
			Assert.AreEqual ("Xamarin.Android.Net.AndroidMessageHandler", handler.GetType ().FullName);
		}

		private static object? GetHttpMessageHandler (string? typeName)
		{
			var method = typeof (AndroidEnvironment).GetMethod ("GetHttpMessageHandler", BindingFlags.Static | BindingFlags.NonPublic);
			lock (envLock) {
				ClearHttpMessageHandlerTypeCache ();
				Environment.SetEnvironmentVariable (EnvironmentVariable, typeName);
				return method.Invoke (null, null);
			}
		}

		private static void ClearHttpMessageHandlerTypeCache ()
		{
			var cacheField = typeof (AndroidEnvironment).GetField ("httpMessageHandlerType", BindingFlags.Static | BindingFlags.NonPublic)!;
			cacheField.SetValue (null, null);
			var isFirstGetHttpMessageHandlerCallField = typeof (AndroidEnvironment).GetField ("isFirstGetHttpMessageHandlerCall", BindingFlags.Static | BindingFlags.NonPublic)!;
			isFirstGetHttpMessageHandlerCallField.SetValue (null, false);
		}
	}
}
