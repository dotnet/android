using System;
using System.Globalization;

using Android.App;
using Android.Content;
using Android.Runtime;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class ExceptionTest {

		static Java.Lang.Throwable CreateJavaProxyThrowable (Exception e)
		{
			var JavaProxyThrowable_type = typeof (Java.Lang.Object)
				.Assembly
				.GetType ("Android.Runtime.JavaProxyThrowable");
			return (Java.Lang.Throwable) Activator.CreateInstance (JavaProxyThrowable_type, e);
		}

		[Test]
		public void InnerExceptionIsSet ()
		{
			var ex  = new InvalidOperationException ("boo!");
			using (var source = new Java.Lang.Throwable ("detailMessage", CreateJavaProxyThrowable (ex)))
			using (var alias  = new Java.Lang.Throwable (source.Handle, JniHandleOwnership.DoNotTransfer)) {
				Assert.AreEqual ("detailMessage", alias.Message);
				Assert.AreSame (ex, alias.InnerException);
			}
		}
	}
}
