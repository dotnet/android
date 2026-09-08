#nullable enable

using System;
using System.Linq;
using System.Text;

using Java.Interop;
using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
public class JniStringMemberLookupTests : JavaVMFixture
{
	const string FixtureName = "net/dot/jni/test/ObjectHelper$StringLookupFixture";

	[TestCase ("constructor", "()V")]
	[TestCase ("instance-method", "m\u00e9thode.()V")]
	[TestCase ("static-method", "m\u00e9thodeStatique.()V")]
	[TestCase ("instance-field", "champ\u00c9.I")]
	[TestCase ("static-field", "champStatique\u00c9.I")]
	public void StringCacheMissMatchesDirectLookup (string kind, string member)
	{
		var members = new JniPeerMembers (FixtureName, typeof (FixturePeer));
		try {
			var expected = LookupDirect (members.JniPeerType, kind, member);
			AssertLookup (member);
			if (kind != "constructor")
				AssertLookup (member.Insert (member.IndexOf ('.'), "\0ignored"));

			// Padding after NUL keeps the native identity but exercises both sides of
			// the combined 512-byte encoding buffer, with distinct managed cache keys.
			int byteCount = Encoding.UTF8.GetByteCount (member) + (kind == "constructor" ? 8 : 1);
			foreach (int bufferLength in new [] { 511, 512, 513, 1024 }) {
				var alias = member + "\0" + new string ('x', bufferLength - byteCount - 1);
				AssertLookup (alias);
			}

			void AssertLookup (string key)
			{
				var first = Lookup (members, kind, key);
				Assert.AreEqual (ID (expected), ID (first));
				Assert.AreSame (first, Lookup (members, kind, key));
#if DEBUG
				var direct = LookupDirect (members.JniPeerType, kind, key);
				Assert.AreEqual (direct.ToString (), first.ToString ());
#endif
			}
		} finally {
			JniPeerMembers.Dispose (members);
		}
	}

	[TestCase ("constructor", "")]
	[TestCase ("instance-method", "longMethod.")]
	[TestCase ("static-method", "longStaticMethod.")]
	public void LongSignatures (string kind, string prefix)
	{
		var signature = "(" + string.Concat (Enumerable.Repeat ("Ljava/lang/Object;", 32)) + ")V";
		var members = new JniPeerMembers (FixtureName, typeof (FixturePeer));
		try {
			var member = prefix + signature;
			var first = Lookup (members, kind, member);
			Assert.AreEqual (ID (LookupDirect (members.JniPeerType, kind, member)), ID (first));
			Assert.AreSame (first, Lookup (members, kind, member));
		} finally {
			JniPeerMembers.Dispose (members);
		}
	}

	[TestCase (null)]
	[TestCase ("")]
	[TestCase ("name")]
	[TestCase ("name.")]
	public void InvalidEncodedMembers (string? member)
	{
		var members = new JniPeerMembers (FixtureName, typeof (FixturePeer));
		try {
			foreach (var kind in new [] { "instance-method", "static-method", "instance-field", "static-field" }) {
				var error = Assert.Catch<ArgumentException> (() => Lookup (members, kind, member));
				Assert.AreEqual (member == null ? "key" : "encodedMember", error?.ParamName);
			}
			if (member == null) {
				var error = Assert.Throws<ArgumentNullException> (() => Lookup (members, "constructor", member));
				Assert.AreEqual ("signature", error?.ParamName);
			}
		} finally {
			JniPeerMembers.Dispose (members);
		}
	}

	[TestCase ("constructor", "")]
	[TestCase ("instance-method", "missing\u00e9\ud800.()V")]
	[TestCase ("static-method", "missing\u00e9\ud800.()V")]
	[TestCase ("instance-field", ".I")]
	[TestCase ("static-field", ".I")]
	public void FailedLookupsDoNotLeavePendingExceptions (string kind, string member)
	{
		var members = new JniPeerMembers (FixtureName, typeof (FixturePeer));
		try {
			foreach (var key in new [] { member, member + "\0" + new string ('x', 1024) }) {
				var expected = Assert.Catch (() => LookupDirect (members.JniPeerType, kind, key));
				for (int i = 0; i < 2; i++) {
					var actual = Assert.Catch (() => Lookup (members, kind, key));
					try {
						Assert.AreEqual (expected?.GetType (), actual?.GetType ());
						Assert.IsFalse (JniEnvironment.Exceptions.ExceptionCheck ());
					} finally {
						(actual as IDisposable)?.Dispose ();
					}
				}
				(expected as IDisposable)?.Dispose ();
			}
			Assert.AreNotEqual (IntPtr.Zero, members.InstanceMethods.GetConstructor ("()V").ID);
		} finally {
			JniPeerMembers.Dispose (members);
		}
	}

	[TestCase (false)]
	[TestCase (true)]
	public void EmptyMethodNamePreservesValidation (bool isStatic)
	{
		var members = new JniPeerMembers (FixtureName, typeof (FixturePeer));
		try {
			var error = Assert.Throws<ArgumentNullException> (() => Lookup (
				members, isStatic ? "static-method" : "instance-method", ".()V"));
			Assert.AreEqual ("jniMethodName", error?.ParamName);
		} finally {
			JniPeerMembers.Dispose (members);
		}
	}

	static object Lookup (JniPeerMembers members, string kind, string? member)
	{
		// Intentionally pass null to the public APIs to check their validation.
#pragma warning disable CS8604
		return kind switch {
			"constructor" => members.InstanceMethods.GetConstructor (member),
			"instance-method" => members.InstanceMethods.GetMethodInfo (member),
			"static-method" => members.StaticMethods.GetMethodInfo (member),
			"instance-field" => members.InstanceFields.GetFieldInfo (member),
			"static-field" => members.StaticFields.GetFieldInfo (member),
			_ => throw new ArgumentOutOfRangeException (nameof (kind)),
		};
#pragma warning restore CS8604
	}

	static object LookupDirect (JniType type, string kind, string member)
	{
		if (kind == "constructor")
			return type.GetConstructor (member);
		int separator = member.IndexOf ('.');
		string name = member.Substring (0, separator);
		string signature = member.Substring (separator + 1);
		return kind switch {
			"instance-method" => type.GetInstanceMethod (name, signature),
			"static-method" => type.GetStaticMethod (name, signature),
			"instance-field" => type.GetInstanceField (name, signature),
			"static-field" => type.GetStaticField (name, signature),
			_ => throw new ArgumentOutOfRangeException (nameof (kind)),
		};
	}

	static IntPtr ID (object value) => value switch {
		JniMethodInfo method => method.ID,
		JniFieldInfo field => field.ID,
		_ => throw new ArgumentException ("Not a JNI member.", nameof (value)),
	};

	[JniTypeSignature (FixtureName, GenerateJavaPeer = false)]
	sealed class FixturePeer : JavaObject
	{
	}
}
