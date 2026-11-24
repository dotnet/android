using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	public class DexUtilsTests
	{
		[Test]
		public void ContainsClassWithFieldMatchesDeclaringClassNameAndType ()
		{
			var dexDump = new [] {
				"  Class descriptor  : 'Lexample/Peer;'",
				"      name          : 'ACTION_COLLAPSE'",
				"      type          : 'Lexample/Peer;'",
				"      name          : 'OTHER'",
				"      type          : 'I'",
				"  Class descriptor  : 'Lexample/Other;'",
				"      name          : 'ACTION_COLLAPSE'",
				"      type          : 'I'",
			};

			Assert.IsTrue (DexUtils.ContainsClassWithField ("Lexample/Peer;", "ACTION_COLLAPSE", "Lexample/Peer;", dexDump));
			Assert.IsFalse (DexUtils.ContainsClassWithField ("Lexample/Peer;", "ACTION_COLLAPSE", "I", dexDump));
			Assert.IsFalse (DexUtils.ContainsClassWithField ("Lexample/Peer;", "COLLAPSE", "Lexample/Peer;", dexDump));
			Assert.IsFalse (DexUtils.ContainsClassWithField ("Lexample/Other;", "ACTION_COLLAPSE", "Lexample/Peer;", dexDump));
		}
	}
}
