using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

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
			.Single (m => m.Name == "GetReplacementMethodInfoCore" && m.GetParameters () [0].ParameterType.IsPointer);
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

	[TestCase (nameof (JniRuntime.ReplacementMethodInfo.TargetJniTypeUtf8Pointer))]
	[TestCase (nameof (JniRuntime.ReplacementMethodInfo.TargetJniMethodNameUtf8Pointer))]
	[TestCase (nameof (JniRuntime.ReplacementMethodInfo.TargetJniMethodSignatureUtf8Pointer))]
	public void BorrowedRemappingSettersRequireUnsafe (string name)
	{
		var property = typeof (JniRuntime.ReplacementMethodInfo).GetProperty (name)
			?? throw new InvalidOperationException ($"Missing remapping pointer property {name}.");
		var setter = property.SetMethod ?? throw new InvalidOperationException ("Missing pointer setter.");
		var getter = property.GetMethod ?? throw new InvalidOperationException ("Missing pointer getter.");
		Assert.AreEqual (typeof (byte*), property.PropertyType);
		Assert.IsTrue (RequiresUnsafe (setter));
		Assert.IsFalse (RequiresUnsafe (getter), "Reading an address does not dereference its storage.");
	}

	[Test]
	public void BorrowedTypeNameProducerRequiresUnsafe ()
	{
		var method = typeof (JniRuntime.JniTypeManager).GetMethod (
			"GetReplacementTypeInfoPointerCore", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException ("Missing borrowed type-name producer.");
		Assert.IsTrue (RequiresUnsafe (method));
		Assert.AreEqual (typeof (byte*).MakeByRefType (), method.GetParameters () [2].ParameterType);
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
	public void LegacyRemappingAccessorsRemainSafeAndWarningObsolete (string name)
	{
		var property = typeof (JniRuntime.ReplacementMethodInfo).GetProperty (name)
			?? throw new InvalidOperationException ($"Missing legacy pointer property {name}.");
		Assert.AreEqual (typeof (IntPtr), property.PropertyType);
		var obsolete = property.GetCustomAttribute<ObsoleteAttribute> ()
			?? throw new InvalidOperationException ("Legacy pointer property must be obsolete.");
		Assert.IsFalse (obsolete.IsError);
		var getter = property.GetMethod ?? throw new InvalidOperationException ("Missing legacy getter.");
		var setter = property.SetMethod ?? throw new InvalidOperationException ("Missing legacy setter.");
		Assert.IsFalse (RequiresUnsafe (getter));
		Assert.IsFalse (RequiresUnsafe (setter));
	}

	[TestCase ("GetReplacementTypeInfoCore")]
	[TestCase ("GetReplacementMethodInfoCore")]
	public void LegacyRemappingHooksRemainSafeAndWarningObsolete (string name)
	{
		var method = typeof (JniRuntime.JniTypeManager).GetMethods (BindingFlags.Instance | BindingFlags.NonPublic)
			.Single (m => m.Name == name && m.GetParameters ().Any (p =>
				p.ParameterType == typeof (IntPtr) || p.ParameterType == typeof (IntPtr).MakeByRefType ()));
		var obsolete = method.GetCustomAttribute<ObsoleteAttribute> ()
			?? throw new InvalidOperationException ("Legacy pointer hook must be obsolete.");
		Assert.IsFalse (obsolete.IsError);
		Assert.IsTrue (method.IsVirtual);
		Assert.IsFalse (RequiresUnsafe (method));
	}

	[Test]
	public void LegacyRemappingPropertiesShareTypedStorage ()
	{
		var address = new IntPtr (1);
#pragma warning disable CS0618 // Exercise the preserved native-int accessors without dereferencing them.
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
	public void NativeMethodConstructorRequiresUnsafeAndPreservesLayout ()
	{
		var constructor = typeof (JniNativeMethod).GetConstructor ([typeof (byte*), typeof (byte*), typeof (IntPtr)])
			?? throw new InvalidOperationException ("Missing raw native registration constructor.");
		Assert.IsTrue (RequiresUnsafe (constructor));
		Assert.AreEqual (3 * IntPtr.Size, Marshal.SizeOf<JniNativeMethod> ());
		Assert.AreEqual (IntPtr.Zero, Marshal.OffsetOf<JniNativeMethod> ("name"));
		Assert.AreEqual (new IntPtr (IntPtr.Size), Marshal.OffsetOf<JniNativeMethod> ("signature"));
		Assert.AreEqual (new IntPtr (2 * IntPtr.Size), Marshal.OffsetOf<JniNativeMethod> ("functionPointer"));
		unsafe {
			fixed (byte* name = "method"u8)
			fixed (byte* signature = "()V"u8) {
				_ = new JniNativeMethod (name, signature,
					(IntPtr) (delegate* unmanaged<IntPtr, IntPtr, void>) &NoOpCallback);
			}
		}
	}

	[TestCase (typeof (JniPeerMembers), "jniPeerTypeNameUtf8", typeof (byte))]
	[TestCase (typeof (JniArrayElements), "elements", typeof (void))]
	[TestCase (typeof (JniNativeMethod), "name", typeof (byte))]
	[TestCase (typeof (JniNativeMethod), "signature", typeof (byte))]
	[TestCase (typeof (JniNativeMethod), "functionPointer", typeof (void))]
	[Category ("NativeAOTIgnore")]
	public void NativeBufferStorageUsesPointerTypes (
		[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicFields | DynamicallyAccessedMemberTypes.NonPublicFields)] Type type,
		string fieldName, Type elementType)
	{
		var field = type.GetField (fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
			?? throw new InvalidOperationException ($"Missing native storage field {fieldName}.");
		Assert.AreEqual (elementType.MakePointerType (), field.FieldType);
	}

	[Test]
	[Category ("NativeAOTIgnore")]
	public void ThreadAttachNameUsesBytePointer ()
	{
		var type = Type.GetType ("Java.Interop.JavaVMThreadAttachArgs, Java.Interop", throwOnError: true)
			?? throw new InvalidOperationException ("Missing thread attach arguments.");
		var field = type.GetField ("name", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException ("Missing thread attach name.");
		Assert.AreEqual (typeof (byte*), field.FieldType);
	}

	[Test]
	[Category ("NativeAOTIgnore")]
	public void BorrowedMethodMetadataConstructorsRequireUnsafe ()
	{
		var constructors = typeof (JniMethodInfo).GetConstructors (BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.AreEqual (3, constructors.Length);
		foreach (var constructor in constructors) {
			Assert.IsTrue (RequiresUnsafe (constructor));
			Assert.IsTrue (constructor.GetParameters ().Take (2).Any (p => p.ParameterType == typeof (byte*)));
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

	[Test]
	[Category ("NativeAOTIgnore")]
	public void RawArrayOwnerRequiresUnsafeButAcquisitionRemainsSafe ()
	{
		var constructor = typeof (JniArrayElements).GetConstructors (BindingFlags.Instance | BindingFlags.NonPublic).Single ();
		Assert.IsTrue (RequiresUnsafe (constructor));
		Assert.AreEqual (typeof (void*), constructor.GetParameters () [0].ParameterType);
		var acquisition = typeof (JavaInt32Array).GetMethod (nameof (JavaInt32Array.GetElements))
			?? throw new InvalidOperationException ("Missing managed array acquisition.");
		Assert.IsFalse (RequiresUnsafe (acquisition));
	}

	static bool RequiresUnsafe (MethodBase method)
	{
		return method.GetCustomAttributesData ().Any (
			a => a.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.RequiresUnsafeAttribute");
	}

	[UnmanagedCallersOnly]
	static void NoOpCallback (IntPtr environment, IntPtr receiver) { }

	class DiagnosticTypeManager : JniRuntime.JniTypeManager {

		public JniRuntime.ReplacementMethodInfo? LookupLiteral ()
		{
			unsafe {
				fixed (byte* name = "example/Source"u8) {
					return GetReplacementMethodInfoCore (name, "method".AsSpan (), "()V".AsSpan ());
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
		protected override unsafe void GetReplacementTypeInfoPointerCore (
			string jniSimpleReference, [MaybeNull] out string replacement, out byte* replacementUtf8)
		{
			unsafe {
				base.GetReplacementTypeInfoPointerCore (jniSimpleReference, out replacement, out replacementUtf8);
			}
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
