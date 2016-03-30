using System;
using System.IO;
using System.Linq;
using System.Reflection;

using Xamarin.Android.Tools.LogcatParse;

using NUnit.Framework;

namespace Xamarin.Android.Tools.LogcatParse.Tests {

	[TestFixture]
	public class JniHandleInfoTests {

		[Test]
		public void ImplicitFromString ()
		{
			JniHandleInfo h     = "0x1234/G";

			Assert.AreEqual ("0x1234",              h.Handle);
			Assert.AreEqual (JniHandleType.Global,  h.Type);
		}
	}
}

