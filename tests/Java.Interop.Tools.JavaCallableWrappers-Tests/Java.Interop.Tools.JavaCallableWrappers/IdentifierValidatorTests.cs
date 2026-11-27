using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Java.Interop.Tools.JavaCallableWrappers;
using Java.Interop.Tools.TypeNameMappings;
using NUnit.Framework;

namespace Java.Interop.Tools.JavaCallableWrappersTests
{
	[TestFixture]
	public class IdentifierValidatorTests
	{
		[Test]
		public void CreateValidIdentifier_Simple ()
		{
			Assert.AreEqual ("my_identifier_test", IdentifierValidator.CreateValidIdentifier ("my-identifier$test"));
		}

		[Test]
		public void CreateValidIdentifier_Encoded ()
		{
			Assert.AreEqual ("my_x45_identifier_x36_test", IdentifierValidator.CreateValidIdentifier ("my-identifier$test", true));
			Assert.AreEqual ("myidentifier_x55357__x56842_test", IdentifierValidator.CreateValidIdentifier ("myidentifier😊test", true));
		}

		[Test]
		public void IsValidIdentifier ()
		{
			Assert.IsTrue (IdentifierValidator.IsValidIdentifier ("name"));
			Assert.IsTrue (IdentifierValidator.IsValidIdentifier ("Name_With_Underscores"));

			// Yes, this is "wrong" -- keywords aren't identifiers -- but the keyword check is done elsewhere.
			Assert.IsTrue (IdentifierValidator.IsValidIdentifier ("true"));

			Assert.IsFalse (IdentifierValidator.IsValidIdentifier ("name-with-hyphens and spaces"));
			Assert.IsFalse (IdentifierValidator.IsValidIdentifier ("123"));
		}
	}
}
