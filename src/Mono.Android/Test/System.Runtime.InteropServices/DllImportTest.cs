using System;
using System.Runtime.InteropServices;

using NUnit.Framework;

namespace Xamarin.Android.RuntimeTests {

	[TestFixture]
	public class DllImportTest {

		[DllImport ("libdl")]
		static extern IntPtr dlopen (string libName, int flags);

		[Test]
		public void PInvokeDlopenSucceeds ()
		{
			const int RTLD_LAZY = 0x001;
			Assert.DoesNotThrow (() => dlopen (null, RTLD_LAZY));
		}
	}
}
