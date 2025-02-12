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
		[TestCase ("Xamarin.Android.Net.AndroidHttpResponseMessage")] // does not extend HttpMessageHandler
		// instantiating AndroidClientHandler or HttpClientHandler (or any other type extending HttpClientHandler)
		// would cause infinite recursion in the .NET build and so it is replaced with AndroidMessageHandler
		[TestCase ("System.Net.Http.HttpClientHandler, System.Net.Http")]
		[TestCase ("Xamarin.Android.Net.AndroidClientHandler")]
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
		[TestCase ("System.Net.Http.SocketsHttpHandler, System.Net.Http")]
		public void GetHttpMessageHandler_OverridesDefaultValue (string typeName)
		{
			var handler = GetHttpMessageHandler (typeName);

			Assert.IsNotNull (handler, "GetHttpMessageHandler returned null");

			// type's FullName doesn't contain the assembly name
			var indexOfComma = typeName.IndexOf(',');
			var expectedTypeName = indexOfComma > 0 ? typeName.Substring(0, indexOfComma) : typeName;
			Assert.AreEqual (expectedTypeName, handler.GetType ().FullName);
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
		}
	}
}
