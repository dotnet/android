using System;
using System.Runtime.InteropServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.RuntimeTests {

	[TestClass]
	public class DllImportTest {

		[DllImport ("libdl")]
		static extern IntPtr dlopen (string libName, int flags);

		[TestMethod]
		public void PInvokeDlopenSucceeds ()
		{
			const int RTLD_LAZY = 0x001;
			global::Xamarin.Android.UnitTests.AndroidTestAssert.DoesNotThrow (() => dlopen (null, RTLD_LAZY));
		}
	}
}
