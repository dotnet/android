using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

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
		JniRuntime.ReplacementMethodInfo info;
		unsafe {
			info = new JniRuntime.ReplacementMethodInfo {
				SourceJniType = "example/Source",
				TargetJniType = "example/Target",
				TargetJniTypeUtf8Pointer = (byte*) pointer,
				TargetJniMethodNameUtf8Pointer = (byte*) pointer,
				TargetJniMethodSignatureUtf8Pointer = (byte*) pointer,
			};
		}

		var text = info.ToString ();
		var formatted = $"\"0x{pointer:x}\"";
		Assert.That (text, Does.Contain ($"TargetJniTypeUtf8 = {formatted}"));
		Assert.That (text, Does.Contain ($"TargetJniMethodNameUtf8 = {formatted}"));
		Assert.That (text, Does.Contain ($"TargetJniMethodSignatureUtf8 = {formatted}"));
		Assert.That (text, Does.Contain ("TargetJniType = \"example/Target\""));
		Assert.IsTrue (info.Equals (info));
		Assert.AreEqual (info.GetHashCode (), info.GetHashCode ());
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

	[TestCase (false)]
	[TestCase (true)]
	public void ManagedTypeLookupPreservesNativePointerPrecedence (bool useNative)
	{
		using JniRuntime.JniTypeManager manager = useNative ? new NativeTypeNameManager () : new ManagedTypeNameManager ();
		Assert.AreEqual (useNative ? "example/Native" : "example/Managed", manager.GetReplacementType ("example/Source"));
	}

	[TestCase ("TargetJniTypeUtf8")]
	[TestCase ("TargetJniMethodNameUtf8")]
	[TestCase ("TargetJniMethodSignatureUtf8")]
	public void LegacyRemappingAccessorsKeepReleasedShape (string name)
	{
		var property = typeof (JniRuntime.ReplacementMethodInfo).GetProperty (name)
			?? throw new InvalidOperationException ($"Missing legacy pointer property {name}.");
		Assert.AreEqual (typeof (IntPtr), property.PropertyType);
		var obsolete = property.GetCustomAttribute<ObsoleteAttribute> ()
			?? throw new InvalidOperationException ("Legacy pointer property must be obsolete.");
		Assert.IsFalse (obsolete.IsError);
		Assert.IsNotNull (property.GetMethod);
		Assert.IsNotNull (property.SetMethod);
	}

	[TestCase ("GetReplacementTypeInfoCore")]
	[TestCase ("GetReplacementMethodInfoCore")]
	public void LegacyRemappingHooksKeepReleasedVirtualSlots (string name)
	{
		var method = typeof (JniRuntime.JniTypeManager).GetMethods (BindingFlags.Instance | BindingFlags.NonPublic)
			.Single (m => m.Name == name && m.GetParameters ().Any (p =>
				p.ParameterType == typeof (IntPtr) || p.ParameterType == typeof (IntPtr).MakeByRefType ()));
		var obsolete = method.GetCustomAttribute<ObsoleteAttribute> ()
			?? throw new InvalidOperationException ("Legacy pointer hook must be obsolete.");
		Assert.IsFalse (obsolete.IsError);
		Assert.IsTrue (method.IsVirtual);
		Assert.IsFalse (method.IsFinal);
	}

	[Test]
	public void LegacyRemappingPropertiesShareTypedStorage ()
	{
		var address = new IntPtr (1);
#pragma warning disable CS0618 // Exercise the preserved safe native-int accessors without dereferencing them.
		var legacy = new JniRuntime.ReplacementMethodInfo {
			TargetJniTypeUtf8 = address,
			TargetJniMethodNameUtf8 = address,
			TargetJniMethodSignatureUtf8 = address,
		};
		var typed = new JniRuntime.ReplacementMethodInfo ();
		unsafe {
			typed.TargetJniTypeUtf8Pointer = (byte*) address;
			typed.TargetJniMethodNameUtf8Pointer = (byte*) address;
			typed.TargetJniMethodSignatureUtf8Pointer = (byte*) address;
			Assert.IsTrue (legacy.TargetJniTypeUtf8Pointer == typed.TargetJniTypeUtf8Pointer);
			Assert.IsTrue (legacy.TargetJniMethodNameUtf8Pointer == typed.TargetJniMethodNameUtf8Pointer);
			Assert.IsTrue (legacy.TargetJniMethodSignatureUtf8Pointer == typed.TargetJniMethodSignatureUtf8Pointer);
		}
		Assert.AreEqual (address, typed.TargetJniTypeUtf8);
		Assert.AreEqual (address, typed.TargetJniMethodNameUtf8);
		Assert.AreEqual (address, typed.TargetJniMethodSignatureUtf8);
#pragma warning restore CS0618
		Assert.IsTrue (legacy.Equals (typed));
		Assert.AreEqual (legacy.GetHashCode (), typed.GetHashCode ());
		Assert.AreEqual (legacy.ToString (), typed.ToString ());
	}

	[Test]
	public void TypedTypeLookupPreservesLegacyOverrideDispatch ()
	{
		using var manager = new LegacyTypeNameManager ();
		Assert.AreEqual ("example/Legacy", manager.GetReplacementType ("example/Source"));
		Assert.AreEqual (1, manager.LegacyCalls);
	}

	[Test]
	public void TypedMethodLookupPreservesLegacyOverrideAndBaseDispatch ()
	{
		using var manager = new LegacyMethodManager ();
		var result = manager.LookupLiteral ().GetValueOrDefault ();
		Assert.AreEqual (1, manager.LegacyCalls);
		Assert.AreEqual ("example/Source", result.SourceJniType);
		Assert.AreEqual ("method", result.SourceJniMethodName);
		Assert.AreEqual ("()V", result.SourceJniMethodSignature);
	}

	[Test]
	[Category ("NativeAOTIgnore")]
	public void NativeBufferStorageUsesPointerTypes ()
	{
		var field = typeof (JniPeerMembers).GetField ("jniPeerTypeNameUtf8", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException ("Missing borrowed peer-name storage.");
		Assert.AreEqual (typeof (byte*), field.FieldType);
		var constructors = typeof (JniMethodInfo).GetConstructors (BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.AreEqual (3, constructors.Length);
		foreach (var constructor in constructors) {
			Assert.IsTrue (constructor.GetParameters ().Take (2).Any (p => p.ParameterType == typeof (byte*)));
			Assert.AreEqual (typeof (IntPtr), constructor.GetParameters () [2].ParameterType);
		}
		Assert.AreEqual (typeof (IntPtr), typeof (JniMethodInfo).GetProperty (nameof (JniMethodInfo.ID))?.PropertyType);
	}

	[TestCase (null, "example/Source")]
	[TestCase ("example/Managed", "example/Managed")]
	[Category ("NativeAOTIgnore")]
	public void ManagedPeerNamePreservesFallback (string target, string expected)
	{
		using var owner = new PeerNameOwner ();
		var constructor = owner.DescriptorType.GetConstructor ([typeof (string), typeof (string)])
			?? throw new InvalidOperationException ("Missing managed peer-name constructor.");
		owner.Create (constructor.Invoke (["example/Source", target]));
		Assert.AreEqual (expected, owner.Members.JniPeerTypeName);
	}

	[Test]
	[Category ("NativeAOTIgnore")]
	public void BorrowedPeerNameIsConvertedAndCachedLazily ()
	{
		unsafe {
			fixed (byte* target = "example/N\u00E1tive"u8) {
				using var owner = new PeerNameOwner ();
				var constructor = owner.DescriptorType.GetConstructor ([typeof (string), typeof (byte*)])
					?? throw new InvalidOperationException ("Missing borrowed peer-name constructor.");
				owner.Create (constructor.Invoke (["example/Source", Pointer.Box (target, typeof (byte*))]));
				var cache = typeof (JniPeerMembers).GetField ("jniPeerTypeName", BindingFlags.Instance | BindingFlags.NonPublic)
					?? throw new InvalidOperationException ("Missing managed peer-name cache.");
				Assert.IsNull (cache.GetValue (owner.Members));
				var name = owner.Members.JniPeerTypeName;
				Assert.AreEqual ("example/N\u00E1tive", name);
				Assert.AreSame (name, cache.GetValue (owner.Members));
				Assert.AreSame (name, owner.Members.JniPeerTypeName);
			}
		}
	}

	[TestCase (0)]
	[TestCase (1)]
	[TestCase (2)]
	[Category ("NativeAOTIgnore")]
	public void BorrowedMethodMetadataReadsLiveBuffersLazily (int variant)
	{
		if (typeof (JniMethodInfo).GetField ("name", BindingFlags.Instance | BindingFlags.NonPublic) == null)
			Assert.Ignore ("The loaded Java.Interop library was built without diagnostic method metadata.");

		const string name = "m\u00E9thod";
		const string signature = "()V";
		var parameterTypes = variant switch {
			0 => new [] { typeof (byte*), typeof (string), typeof (IntPtr), typeof (bool) },
			1 => new [] { typeof (byte*), typeof (byte*), typeof (IntPtr), typeof (bool) },
			_ => new [] { typeof (string), typeof (byte*), typeof (IntPtr), typeof (bool) },
		};
		var constructor = typeof (JniMethodInfo).GetConstructor (
			BindingFlags.Instance | BindingFlags.NonPublic, null, parameterTypes, null)
			?? throw new InvalidOperationException ("Missing borrowed metadata constructor.");

		unsafe {
			fixed (byte* namePointer = "m\u00E9thod"u8)
			fixed (byte* signaturePointer = "()V"u8) {
				object nameArgument = variant == 2 ? name : Pointer.Box (namePointer, typeof (byte*));
				object signatureArgument = variant == 0 ? signature : Pointer.Box (signaturePointer, typeof (byte*));
				var info = constructor.Invoke ([nameArgument, signatureArgument, new IntPtr (1), false]) as JniMethodInfo
					?? throw new InvalidOperationException ("Borrowed metadata construction failed.");
				Assert.AreEqual (name, info.Name);
				Assert.AreEqual (signature, info.Signature);
				Assert.That (info.ToString (), Does.Contain ($"Name={name}, Signature=()V"));
			}
		}
	}

	sealed class PeerNameOwner : IDisposable {
		JniPeerMembers members;

		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors)]
		public Type DescriptorType { get; } = typeof (JniPeerMembers).GetNestedType ("JniPeerTypeNameInfo", BindingFlags.NonPublic)
			?? throw new InvalidOperationException ("Missing peer-name descriptor.");

		public JniPeerMembers Members => members ?? throw new InvalidOperationException ("Peer members not created.");

		public void Create (object descriptor)
		{
			var constructor = typeof (JniPeerMembers).GetConstructor (BindingFlags.Instance | BindingFlags.NonPublic,
				null, [DescriptorType, typeof (Type), typeof (bool), typeof (bool)], null)
				?? throw new InvalidOperationException ("Missing peer-name initialization constructor.");
			members = constructor.Invoke ([descriptor, typeof (IJavaPeerable), false, false]) as JniPeerMembers
				?? throw new InvalidOperationException ("Peer members construction failed.");
		}

		public void Dispose () => JniPeerMembers.Dispose (members);
	}

	class DiagnosticTypeManager : JniRuntime.JniTypeManager {
		public JniRuntime.ReplacementMethodInfo? LookupLiteral ()
		{
			unsafe {
				fixed (byte* name = "example/Source"u8) {
					return GetReplacementMethodInfoUnsafe (name, "method".AsSpan (), "()V".AsSpan ());
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

	class ManagedTypeNameManager : JniRuntime.JniTypeManager {
		protected override string GetReplacementTypeCore (string jniSimpleReference) => "example/Managed";
	}

	sealed class NativeTypeNameManager : ManagedTypeNameManager {
		protected override unsafe void GetReplacementTypeInfoUnsafe (
			string jniSimpleReference, [MaybeNull] out string replacement, out byte* replacementUtf8)
		{
			base.GetReplacementTypeInfoUnsafe (jniSimpleReference, out replacement, out replacementUtf8);
			fixed (byte* name = "example/Native"u8) {
				replacementUtf8 = name;
			}
		}
	}

	sealed class LegacyTypeNameManager : ManagedTypeNameManager {
		public int LegacyCalls { get; private set; }

		[Obsolete ("Tests legacy override dispatch.")]
		protected override void GetReplacementTypeInfoCore (
			string jniSimpleReference, [MaybeNull] out string replacement, out IntPtr replacementUtf8)
		{
			base.GetReplacementTypeInfoCore (jniSimpleReference, out replacement, out replacementUtf8);
			LegacyCalls++;
			unsafe {
				fixed (byte* name = "example/Legacy"u8) {
					replacementUtf8 = (IntPtr) name;
				}
			}
		}
	}

	sealed class LegacyMethodManager : DiagnosticTypeManager {
		public int LegacyCalls { get; private set; }

		[Obsolete ("Tests legacy override dispatch.")]
		protected override JniRuntime.ReplacementMethodInfo? GetReplacementMethodInfoCore (
			IntPtr jniSimpleReferenceUtf8, ReadOnlySpan<char> jniMethodName, ReadOnlySpan<char> jniMethodSignature)
		{
			LegacyCalls++;
			return base.GetReplacementMethodInfoCore (jniSimpleReferenceUtf8, jniMethodName, jniMethodSignature);
		}
	}
}
