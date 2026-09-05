using NUnit.Framework;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

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

	[Test]
	public void ContainsClassWithMethodRejectsMissingSignature ()
	{
		var dexDump = new [] {
			"  Class descriptor  : 'Lexample/Peer;'\r",
			"      name          : '$default$getValue'\r",
			"      access        : 0x0009 (PUBLIC STATIC)\r",
			"      type          : '()I'\r",
		};

		Assert.IsFalse (DexUtils.ContainsClassWithMethod ("Lexample/Peer;", "$default$getValue", "()I", dexDump));
	}

	[Test]
	public void ContainsClassWithMethodHandlesWhitespaceAndCrLf ()
	{
		var dexDump = new [] {
			"\tClass descriptor  : 'Lexample/Peer$-CC;'  \r",
			"    name : '$default$getValue'\r",
			"      type          : '(Lexample/Peer;)I'\r",
		};

		Assert.IsTrue (DexUtils.ContainsClassWithMethod (
			"Lexample/Peer$-CC;",
			"$default$getValue",
			"(Lexample/Peer;)I",
			dexDump));
	}

	[Test]
	public void ContainsClassWithMethodResetsAtRepeatedClassDescriptor ()
	{
		var dexDump = new [] {
			"  Class descriptor  : 'Lexample/Peer;'",
			"      name          : 'getValue'",
			"  Class descriptor  : 'Lexample/Other;'",
			"      name          : 'getValue'",
			"      type          : '()I'",
		};

		Assert.IsFalse (DexUtils.ContainsClassWithMethod ("Lexample/Peer;", "getValue", "()I", dexDump));
	}
}
