using System;
using System.Runtime.InteropServices;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests;

[TestFixture]
public class JniNativeInterfaceTests
{
	[TestCase (nameof (JNIEnv.NewObject), 28)]
	[TestCase (nameof (JNIEnv.NewObjectV), 29)]
	[TestCase (nameof (JNIEnv.NewObjectA), 30)]
	[TestCase (nameof (JNIEnv.CallObjectMethod), 34)]
	[TestCase (nameof (JNIEnv.CallObjectMethodV), 35)]
	[TestCase (nameof (JNIEnv.CallObjectMethodA), 36)]
	[TestCase (nameof (JNIEnv.GetObjectRefType), 232)]
	public void FunctionPointerOffset (string fieldName, int slot)
	{
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
		var fieldType = typeof (JNIEnv).GetField (fieldName)?.FieldType ??
			throw new InvalidOperationException ($"Could not find JNIEnv field '{fieldName}'.");

		Assert.AreEqual (returnType, fieldType.GetFunctionPointerReturnType ());
		Assert.AreEqual (parameterTypes, fieldType.GetFunctionPointerParameterTypes ());
	}
}
