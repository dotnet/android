using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests
{
	[TestFixture]
	public class DexUtilsTests
	{
		[Test]
		public void ContainsClassWithMethodScopesSignatureToMethod ()
		{
			var dexDump = new [] {
				"  Class descriptor  : 'Lexample/Peer;'",
				"      name          : 'target'",
				"      type          : '(I)V'",
				"      name          : 'other'",
				"      type          : '()V'",
			};

			Assert.IsTrue (DexUtils.ContainsClassWithMethod ("Lexample/Peer;", "target", "(I)V", dexDump));
			Assert.IsFalse (DexUtils.ContainsClassWithMethod ("Lexample/Peer;", "target", "()V", dexDump));
		}

		[Test]
		public void ContainsClassWithMethodMatchesExactNames ()
		{
			var dexDump = new [] {
				"  Class descriptor  : 'Lexample/PeerExtension;'",
				"      name          : 'getValueExtension'",
				"      type          : '()I'",
			};

			Assert.IsFalse (DexUtils.ContainsClassWithMethod ("Lexample/Peer;", "getValue", "()I", dexDump));
		}

		[TestCase ("()Ljava/lang/Object;")]
		[TestCase ("()Ljava/lang/String;")]
		public void ContainsClassWithMethodMatchesOverloads (string signature)
		{
			var dexDump = new [] {
				"  Class descriptor  : 'Lexample/Derived;'",
				"      name          : 'getValue'",
				"      type          : '()Ljava/lang/Object;'",
				"      name          : 'getValue'",
				"      type          : '()Ljava/lang/String;'",
			};

			Assert.IsTrue (DexUtils.ContainsClassWithMethod ("Lexample/Derived;", "getValue", signature, dexDump));
		}
	}
}
