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
				TargetJniTypeUtf8 = (byte*) pointer,
				TargetJniMethodNameUtf8 = (byte*) pointer,
				TargetJniMethodSignatureUtf8 = (byte*) pointer,
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

	[TestCase (nameof (JniRuntime.ReplacementMethodInfo.TargetJniTypeUtf8))]
	[TestCase (nameof (JniRuntime.ReplacementMethodInfo.TargetJniMethodNameUtf8))]
	[TestCase (nameof (JniRuntime.ReplacementMethodInfo.TargetJniMethodSignatureUtf8))]
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
			"GetReplacementTypeInfoCore", BindingFlags.Instance | BindingFlags.NonPublic)
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

#if DEBUG
	[TestCase (0)]
	[TestCase (1)]
	[TestCase (2)]
	[Category ("NativeAOTIgnore")]
	public void BorrowedMethodMetadataReadsLiveBuffersLazily (int variant)
	{
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
#endif

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

	sealed class DiagnosticTypeManager : JniRuntime.JniTypeManager {

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
		protected override unsafe void GetReplacementTypeInfoCore (
			string jniSimpleReference, [MaybeNull] out string replacement, out byte* replacementUtf8)
		{
			unsafe {
				base.GetReplacementTypeInfoCore (jniSimpleReference, out replacement, out replacementUtf8);
			}
			fixed (byte* name = "example/Native"u8) {
				replacementUtf8 = name;
			}
		}
	}
}
