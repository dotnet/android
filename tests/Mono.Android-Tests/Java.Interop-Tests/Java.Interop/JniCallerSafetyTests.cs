using System;
using System.Linq;
using System.Reflection;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
[Category ("JniCallerSafety")]
public class JniCallerSafetyTests {

	[TestCase (0)]
	[TestCase (1)]
	[TestCase (-1)]
	public void RemappingDiagnosticsDoNotReadBorrowedPointers (int address)
	{
		var pointer = new IntPtr (address);
		var info = new JniRuntime.ReplacementMethodInfo {
			SourceJniType = "example/Source",
			TargetJniType = "example/Target",
			TargetJniTypeUtf8 = pointer,
			TargetJniMethodNameUtf8 = pointer,
			TargetJniMethodSignatureUtf8 = pointer,
		};

		var text = info.ToString ();
		var formatted = $"\"0x{pointer:x}\"";
		Assert.That (text, Does.Contain ($"TargetJniTypeUtf8 = {formatted}"));
		Assert.That (text, Does.Contain ($"TargetJniMethodNameUtf8 = {formatted}"));
		Assert.That (text, Does.Contain ($"TargetJniMethodSignatureUtf8 = {formatted}"));
		Assert.That (text, Does.Contain ("TargetJniType = \"example/Target\""));
	}

	[Test]
	public void OwnedRegistrationRequiresUnsafe ()
	{
		var method = typeof (JniType).GetMethod (nameof (JniType.RegisterNativeMethods));
		Assert.IsNotNull (method);
		if (method == null)
			throw new InvalidOperationException ("Missing owned native registration API.");

		Assert.IsTrue (RequiresUnsafe (method), "Rooting does not discharge callback ABI compatibility.");
	}

	[Test]
	public void RawRemappingLookupRequiresUnsafeButDiagnosticsRemainSafe ()
	{
		var method = typeof (JniRuntime.JniTypeManager).GetMethods (BindingFlags.Instance | BindingFlags.NonPublic)
			.Single (m => m.Name == "GetReplacementMethodInfoCore" && m.GetParameters () [0].ParameterType == typeof (IntPtr));
		Assert.IsTrue (RequiresUnsafe (method));

		var toString = typeof (JniRuntime.ReplacementMethodInfo).GetMethod (nameof (JniRuntime.ReplacementMethodInfo.ToString));
		Assert.IsNotNull (toString);
		if (toString == null)
			throw new InvalidOperationException ("Missing remapping diagnostics API.");
		Assert.IsFalse (RequiresUnsafe (toString));
	}

	[Test]
	public void RawRemappingLookupPreservesManagedFallback ()
	{
		using var manager = new DiagnosticTypeManager ();
		var result = manager.LookupLiteral ();
		Assert.IsTrue (result.HasValue);
		Assert.AreEqual ("example/Source", result.GetValueOrDefault ().SourceJniType);
		Assert.AreEqual ("method", result.GetValueOrDefault ().SourceJniMethodName);
		Assert.AreEqual ("()V", result.GetValueOrDefault ().SourceJniMethodSignature);
	}

	static bool RequiresUnsafe (MethodInfo method)
	{
		return method.GetCustomAttributesData ().Any (
			a => a.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.RequiresUnsafeAttribute");
	}

	sealed class DiagnosticTypeManager : JniRuntime.JniTypeManager {

		public JniRuntime.ReplacementMethodInfo? LookupLiteral ()
		{
			unsafe {
				fixed (byte* name = "example/Source"u8) {
					return GetReplacementMethodInfoCore ((IntPtr) name, "method".AsSpan (), "()V".AsSpan ());
				}
			}
		}

		protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (
			string jniSimpleReference, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
		{
			return new JniRuntime.ReplacementMethodInfo {
				SourceJniType = jniSimpleReference,
				SourceJniMethodName = jniMethodName.ToString (),
				SourceJniMethodSignature = jniMethodSignature.ToString (),
			};
		}
	}
}
