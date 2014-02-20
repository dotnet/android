using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaExceptionTests
	{
		[Test]
		public void StackTrace ()
		{
			try {
				new JniType ("this/type/had/better/not/exist");
			} catch (JavaException e) {
				Assert.AreEqual ("this/type/had/better/not/exist", e.Message);
				Assert.IsTrue (e.JavaStackTrace.StartsWith ("java.lang.NoClassDefFoundError: this/type/had/better/not/exist", StringComparison.Ordinal));
				e.Dispose ();
			}
		}
	}
}

