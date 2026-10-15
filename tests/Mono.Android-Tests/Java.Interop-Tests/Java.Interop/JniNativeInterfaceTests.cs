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
		Assert.AreEqual (3 * IntPtr.Size, Marshal.SizeOf<JniNativeMethod> ());
		Assert.AreEqual (IntPtr.Zero, Marshal.OffsetOf<JniNativeMethod> ("name"));
		Assert.AreEqual (new IntPtr (IntPtr.Size), Marshal.OffsetOf<JniNativeMethod> ("signature"));
		Assert.AreEqual (new IntPtr (2 * IntPtr.Size), Marshal.OffsetOf<JniNativeMethod> ("functionPointer"));
	}

	[Test]
	public void ArgumentValueLayout ()
	{
		Assert.AreEqual (8, Marshal.SizeOf<JniArgumentValue> ());
		foreach (var fieldName in new [] { "z", "b", "c", "s", "i", "j", "f", "d", "l" }) {
			Assert.AreEqual (IntPtr.Zero, Marshal.OffsetOf<JniArgumentValue> (fieldName), fieldName);
		}
	}

	[TestCase (nameof (JNIEnv.DefineClass), 5)]
	[TestCase (nameof (JNIEnv.NewObject), 28)]
	[TestCase (nameof (JNIEnv.NewObjectV), 29)]
	[TestCase (nameof (JNIEnv.NewObjectA), 30)]
	[TestCase (nameof (JNIEnv.CallObjectMethod), 34)]
	[TestCase (nameof (JNIEnv.CallObjectMethodV), 35)]
	[TestCase (nameof (JNIEnv.CallObjectMethodA), 36)]
	[TestCase (nameof (JNIEnv.ReleasePrimitiveArrayCritical), 223)]
	[TestCase (nameof (JNIEnv.NewDirectByteBuffer), 229)]
	[TestCase (nameof (JNIEnv.GetObjectRefType), 232)]
	public void FunctionPointerOffset (string fieldName, int slot)
	{
		Assert.AreEqual ((long) slot * IntPtr.Size, Marshal.OffsetOf<JNIEnv> (fieldName).ToInt64 ());
	}

	[Test]
	public void FunctionPointerSignatures ()
	{
		AssertFunctionPointerSignature (nameof (JNIEnv.DefineClass), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr), typeof (int));
		AssertFunctionPointerSignature (nameof (JNIEnv.ExceptionCheck), typeof (byte), typeof (IntPtr));
		AssertFunctionPointerSignature (nameof (JNIEnv.GetJavaVM), typeof (int), typeof (IntPtr), typeof (IntPtr).MakePointerType ());
		AssertFunctionPointerSignature (nameof (JNIEnv.CallObjectMethodA), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr));
		AssertFunctionPointerSignature (nameof (JNIEnv.ReleasePrimitiveArrayCritical), typeof (void), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr), typeof (int));
		AssertFunctionPointerSignature (nameof (JNIEnv.NewDirectByteBuffer), typeof (IntPtr), typeof (IntPtr), typeof (IntPtr), typeof (long));
	}

	static void AssertFunctionPointerSignature (string fieldName, Type returnType, params Type [] parameterTypes)
	{
		var fieldType = typeof (JNIEnv).GetField (fieldName)?.FieldType ??
			throw new InvalidOperationException ($"Could not find JNIEnv field '{fieldName}'.");

		Assert.AreEqual (returnType, fieldType.GetFunctionPointerReturnType ());
		Assert.AreEqual (parameterTypes, fieldType.GetFunctionPointerParameterTypes ());
	}
}
