using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

[TestClass]
[DoNotParallelize]
public class DexUtilsTests
{
	[TestMethod]
	[DataRow ("Class descriptor", "Lexample/Peer$-CC;", true)]
	[DataRow ("Class descriptor", "Lexample/Peer$-CC", false)]
	[DataRow ("Class descriptor", "Peer$-CC;", false)]
	[DataRow ("Class descriptor", "Lexample/Peer$-CC$Nested;", false)]
	[DataRow ("Other Class descriptor", "Lexample/Peer$-CC;", false)]
	[DataRow ("Class descriptor extension", "Lexample/Peer$-CC;", false)]
	public void ContainsClassMatchesExactFieldAndDescriptor (string field, string className, bool expected)
	{
		var dexDump = new [] {
			$"\t{field}  : 'Lexample/Peer$-CC;'  \r",
		};

		Assert.AreEqual (expected, DexUtils.ContainsClass (className, dexDump));
	}

	[TestMethod]
	public void ContainsClassWithFieldMatchesDeclaringClassNameAndType ()
	{
		var dexDump = new [] {
			"  Class descriptor  : 'Lexample/Peer;'",
			"      name          : 'ACTION_COLLAPSE'",
			"      type          : 'Lexample/Peer;'",
			"  Class descriptor  : 'Lexample/Other;'",
			"      name          : 'OTHER'",
			"      type          : 'I'",
		};

		Assert.IsTrue (DexUtils.ContainsClassWithField ("Lexample/Peer;", "ACTION_COLLAPSE", "Lexample/Peer;", dexDump));
		Assert.IsFalse (DexUtils.ContainsClassWithField ("Lexample/Peer;", "ACTION_COLLAPSE", "I", dexDump));
		Assert.IsFalse (DexUtils.ContainsClassWithField ("Lexample/Peer;", "COLLAPSE", "Lexample/Peer;", dexDump));
		Assert.IsFalse (DexUtils.ContainsClassWithField ("Lexample/Other;", "ACTION_COLLAPSE", "Lexample/Peer;", dexDump));
	}

	[TestMethod]
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

	[TestMethod]
	public void ContainsClassWithMethodMatchesExactNames ()
	{
		var dexDump = new [] {
			"  Class descriptor  : 'Lexample/PeerExtension;'",
			"      name          : 'getValueExtension'",
			"      type          : '()I'",
		};

		Assert.IsFalse (DexUtils.ContainsClassWithMethod ("Lexample/Peer;", "getValue", "()I", dexDump));
	}

	[TestMethod]
	[DataRow ("()Ljava/lang/Object;")]
	[DataRow ("()Ljava/lang/String;")]
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

	[TestMethod]
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

	[TestMethod]
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

	[TestMethod]
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
