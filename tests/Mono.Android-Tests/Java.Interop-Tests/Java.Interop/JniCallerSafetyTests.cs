using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
[Category ("JniCallerSafety")]
public class JniCallerSafetyTests {

	[Test]
	public void JavaInteropUsesUpdatedMemorySafetyRules ()
	{
		var rules = typeof (JniRuntime).Module.GetCustomAttributesData ()
			.Single (attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.MemorySafetyRulesAttribute");
		Assert.AreEqual (2, rules.ConstructorArguments [0].Value);
	}

	[Test]
	public void OwnedRegistrationRequiresUnsafe ()
	{
		var method = typeof (JniType).GetMethod (nameof (JniType.RegisterNativeMethods))
			?? throw new InvalidOperationException ("Missing owned native registration API.");
		Assert.IsTrue (RequiresUnsafe (method), "Rooting does not discharge callback ABI compatibility.");
	}

	[Test]
	public void RawRemappingLookupRequiresUnsafeButDiagnosticsRemainSafe ()
	{
		var method = typeof (JniRuntime.JniTypeManager).GetMethods (BindingFlags.Instance | BindingFlags.NonPublic)
			.Single (candidate => candidate.Name == "GetReplacementMethodInfoUnsafe" && candidate.GetParameters () [0].ParameterType.IsPointer);
		Assert.IsTrue (RequiresUnsafe (method));
		var toString = typeof (JniRuntime.ReplacementMethodInfo).GetMethod (nameof (JniRuntime.ReplacementMethodInfo.ToString))
			?? throw new InvalidOperationException ("Missing remapping diagnostics API.");
		Assert.IsFalse (RequiresUnsafe (toString));
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
			"GetReplacementTypeInfoUnsafe", BindingFlags.Instance | BindingFlags.NonPublic)
			?? throw new InvalidOperationException ("Missing borrowed type-name producer.");
		Assert.IsTrue (RequiresUnsafe (method));
		Assert.AreEqual (typeof (byte*).MakeByRefType (), method.GetParameters () [2].ParameterType);
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
		Assert.IsFalse (RequiresUnsafe (property.GetMethod ?? throw new InvalidOperationException ("Missing legacy getter.")));
		Assert.IsFalse (RequiresUnsafe (property.SetMethod ?? throw new InvalidOperationException ("Missing legacy setter.")));
	}

	[TestCase ("GetReplacementTypeInfoCore")]
	[TestCase ("GetReplacementMethodInfoCore")]
	public void LegacyRemappingHooksRemainSafeAndWarningObsolete (string name)
	{
		var method = typeof (JniRuntime.JniTypeManager).GetMethods (BindingFlags.Instance | BindingFlags.NonPublic)
			.Single (candidate => candidate.Name == name && candidate.GetParameters ().Any (parameter =>
				parameter.ParameterType == typeof (IntPtr) || parameter.ParameterType == typeof (IntPtr).MakeByRefType ()));
		var obsolete = method.GetCustomAttribute<ObsoleteAttribute> ()
			?? throw new InvalidOperationException ("Legacy pointer hook must be obsolete.");
		Assert.IsFalse (obsolete.IsError);
		Assert.IsTrue (method.IsVirtual);
		Assert.IsFalse (RequiresUnsafe (method));
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
	}

	[Test]
	[Category ("NativeAOTIgnore")]
	public void BorrowedMethodMetadataConstructorsRequireUnsafe ()
	{
		var constructors = typeof (JniMethodInfo).GetConstructors (BindingFlags.Instance | BindingFlags.NonPublic);
		Assert.AreEqual (3, constructors.Length);
		foreach (var constructor in constructors) {
			Assert.IsTrue (RequiresUnsafe (constructor));
			Assert.IsTrue (constructor.GetParameters ().Take (2).Any (parameter => parameter.ParameterType == typeof (byte*)));
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
		=> method.GetCustomAttributesData ().Any (
			attribute => attribute.AttributeType.FullName == "System.Diagnostics.CodeAnalysis.RequiresUnsafeAttribute");
}
