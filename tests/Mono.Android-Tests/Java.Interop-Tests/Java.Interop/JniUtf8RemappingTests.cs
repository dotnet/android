using System;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests;

[TestClass]
[TestCategory ("JniUtf8Remapping")]
public class JniUtf8RemappingTests {

	[TestMethod]
	[DataRow (0)]
	[DataRow (1)]
	[DataRow (-1)]
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
		StringAssert.Contains (text, $"TargetJniTypeUtf8 = {formatted}");
		StringAssert.Contains (text, $"TargetJniMethodNameUtf8 = {formatted}");
		StringAssert.Contains (text, $"TargetJniMethodSignatureUtf8 = {formatted}");
		StringAssert.Contains (text, "TargetJniType = \"example/Target\"");
	}
}
