using System;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
public class JniNativeInterfaceTests
{
	[Test]
	public void NativeMethodLayout ()
	{
		// SAFETY: Inspect layout metadata only; no descriptor buffers are read and no callback is invoked.
		Assert.AreEqual (3 * IntPtr.Size, Marshal.SizeOf<JniNativeMethod> ());
		Assert.AreEqual (IntPtr.Zero, Marshal.OffsetOf<JniNativeMethod> ("name"));
		Assert.AreEqual (new IntPtr (IntPtr.Size), Marshal.OffsetOf<JniNativeMethod> ("signature"));
		Assert.AreEqual (new IntPtr (2 * IntPtr.Size), Marshal.OffsetOf<JniNativeMethod> ("functionPointer"));
	}

	[Test]
	public void ArgumentValueLayout ()
	{
		// SAFETY: Query scalar-union offsets without converting bits to managed references or dereferencing handles.
		Assert.AreEqual (8, Marshal.SizeOf<JniArgumentValue> ());
		foreach (var fieldName in new [] { "z", "b", "c", "s", "i", "j", "f", "d", "l" }) {
			Assert.AreEqual (IntPtr.Zero, Marshal.OffsetOf<JniArgumentValue> (fieldName), fieldName);
		}
	}

	[TestCase (nameof (JNIEnv.NewObject), 28)]
	[TestCase (nameof (JNIEnv.NewObjectV), 29)]
	[TestCase (nameof (JNIEnv.NewObjectA), 30)]
	[TestCase (nameof (JNIEnv.CallObjectMethod), 34)]
	[TestCase (nameof (JNIEnv.CallObjectMethodV), 35)]
	[TestCase (nameof (JNIEnv.CallObjectMethodA), 36)]
	[TestCase (nameof (JNIEnv.GetObjectRefType), 232)]
	public void FunctionPointerOffset (string fieldName, int slot)
	{
		// SAFETY: Compare metadata offsets; no live JNIEnv, vtable read, or native call is required.
		Assert.AreEqual ((long) slot * IntPtr.Size, Marshal.OffsetOf<JNIEnv> (fieldName).ToInt64 ());
	}

	[Test]
	public void FunctionPointerSignatures ()
	{
		AssertFunctionPointerSignature (nameof (JNIEnv.ExceptionCheck), typeof (byte), typeof (IntPtr));
		AssertFunctionPointerSignature (nameof (JNIEnv.GetJavaVM), typeof (int), typeof (IntPtr), typeof (IntPtr).MakePointerType ());
		AssertFunctionPointerSignature (nameof (JNIEnv.CallObjectMethodA), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr));
	}

	static void AssertFunctionPointerSignature (string fieldName, Type returnType, params Type [] parameterTypes)
	{
		// SAFETY: Reflect only the field's signature type; never load or invoke a function-pointer value.
		var fieldType = typeof (JNIEnv).GetField (fieldName)?.FieldType ??
			throw new InvalidOperationException ($"Could not find JNIEnv field '{fieldName}'.");

		Assert.AreEqual (returnType, fieldType.GetFunctionPointerReturnType ());
		Assert.AreEqual (parameterTypes, fieldType.GetFunctionPointerParameterTypes ());
	}
}
