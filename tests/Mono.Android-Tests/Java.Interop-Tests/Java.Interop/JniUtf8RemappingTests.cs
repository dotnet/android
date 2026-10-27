using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
[Category ("JniUtf8Remapping")]
public class JniUtf8RemappingTests {

	[TestCase (0)]
	[TestCase (1)]
	[TestCase (-1)]
	public void RemappingDiagnosticsDoNotReadBorrowedPointers (int address)
	{
		var pointer = new IntPtr (address);
#pragma warning disable CS0618 // Exercise released address setters without dereferencing the sentinel values.
		var info = new JniRuntime.ReplacementMethodInfo {
			SourceJniType = "example/Source",
			TargetJniType = "example/Target",
			TargetJniTypeUtf8 = pointer,
			TargetJniMethodNameUtf8 = pointer,
			TargetJniMethodSignatureUtf8 = pointer,
		};
#pragma warning restore CS0618

		var text = info.ToString ();
		var formatted = $"\"0x{pointer:x}\"";
		Assert.That (text, Does.Contain ($"TargetJniTypeUtf8 = {formatted}"));
		Assert.That (text, Does.Contain ($"TargetJniMethodNameUtf8 = {formatted}"));
		Assert.That (text, Does.Contain ($"TargetJniMethodSignatureUtf8 = {formatted}"));
		Assert.That (text, Does.Contain ("TargetJniType = \"example/Target\""));
	}
}
