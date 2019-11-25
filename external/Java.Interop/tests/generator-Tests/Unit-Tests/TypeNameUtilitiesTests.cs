using System;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	[TestFixture]
	public class TypeNameUtilitiesTests
	{
		[Test]
		public void MangleName ()
		{
			Assert.AreEqual ("@abstract", TypeNameUtilities.MangleName ("abstract"));
			Assert.AreEqual ("@case", TypeNameUtilities.MangleName ("case"));
			Assert.AreEqual ("@namespace", TypeNameUtilities.MangleName ("namespace"));
			Assert.AreEqual ("@while", TypeNameUtilities.MangleName ("while"));

			Assert.AreEqual ("e", TypeNameUtilities.MangleName ("event"));
			Assert.AreEqual ("byte_var", TypeNameUtilities.MangleName ("byte_var"));
			Assert.AreEqual ("foo", TypeNameUtilities.MangleName ("foo"));
		}
	}
}
