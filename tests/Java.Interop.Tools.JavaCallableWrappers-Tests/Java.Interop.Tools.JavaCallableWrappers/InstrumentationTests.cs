using System;
using System.Linq;

using Android.Runtime;

using NUnit.Framework;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestFixture]
	public class InstrumentationTests
	{
		[Test]
		public void OnCreateUsesBundleSignature ()
		{
			var method = typeof (Android.App.Instrumentation).GetMethod ("OnCreate") ?? throw new InvalidOperationException ("Instrumentation is missing OnCreate.");
			var registration = method.GetCustomAttributes (typeof (RegisterAttribute), false).Cast<RegisterAttribute> ().Single ();

			CollectionAssert.AreEqual (new [] { typeof (Android.OS.Bundle) },
				method.GetParameters ().Select (parameter => parameter.ParameterType));
			Assert.AreEqual (typeof (void), method.ReturnType);
			Assert.AreEqual ("onCreate", registration.Name);
			Assert.AreEqual ("(Landroid/os/Bundle;)V", registration.Signature);
		}
	}
}
