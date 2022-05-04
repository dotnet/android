using System.Collections.Generic;
using System.Linq;
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

		[Test, TestCaseSource (nameof (ReservedKeywords))]
		[SetCulture ("cs-CZ")]
		public void MangleNameCutlureInvariant (string keyword)
		{
			Assert.AreEqual ($"@{keyword}", TypeNameUtilities.MangleName (keyword));
		}

		private static IEnumerable<TestCaseData> ReservedKeywords
			=> TypeNameUtilities.reserved_keywords
				.Where (keyword => keyword != "event") // "event" is a special case which is mapped to "e" instead of "@event"
				.Select (keyword => new TestCaseData (keyword));
	}
}
