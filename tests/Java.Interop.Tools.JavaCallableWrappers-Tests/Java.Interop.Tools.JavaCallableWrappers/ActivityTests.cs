using System;
using System.Linq;

using Android.Runtime;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestClass]
	public class ActivityTests
	{
		[TestMethod]
		public void OnCreateUsesBundleSignature ()
		{
			var method = typeof (Android.App.Activity).GetMethod ("OnCreate") ?? throw new InvalidOperationException ("Activity is missing OnCreate.");
			var registration = method.GetCustomAttributes (typeof (RegisterAttribute), false).Cast<RegisterAttribute> ().Single ();

			CollectionAssert.AreEqual (new [] { typeof (Android.OS.Bundle) }, 			method.GetParameters ().Select (parameter => parameter.ParameterType).ToArray ());
			Assert.AreEqual (typeof (void), method.ReturnType);
			Assert.AreEqual ("onCreate", registration.Name);
			Assert.AreEqual ("(Landroid/os/Bundle;)V", registration.Signature);
		}
	}
}
