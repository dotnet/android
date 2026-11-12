using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MonoDroid.Generation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	[TestClassAttribute]
	public class TypeNameUtilitiesTests
	{
		[TestMethodAttribute]
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

		[TestMethodAttribute, DynamicData (nameof (ReservedKeywords))]
		public void MangleNameCutlureInvariant (string keyword)
		{
			var culture = CultureInfo.CurrentCulture;
			try {
				CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo ("cs-CZ");
				Assert.AreEqual ($"@{keyword}", TypeNameUtilities.MangleName (keyword));
			} finally {
				CultureInfo.CurrentCulture = culture;
			}
		}

		public static IEnumerable<object []> ReservedKeywords
			=> TypeNameUtilities.reserved_keywords
				.Where (keyword => keyword != "event") // "event" is a special case which is mapped to "e" instead of "@event"
				.Select (keyword => new object [] { keyword });
	}
}
