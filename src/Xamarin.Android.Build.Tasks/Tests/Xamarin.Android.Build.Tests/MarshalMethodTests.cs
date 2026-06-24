#nullable enable
using System;
using System.IO;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using NUnit.Framework;
using Xamarin.Android.Tasks;
using Xamarin.ProjectTools;

namespace Xamarin.Android.Build.Tests;

public class MarshalMethodTests : BaseTest
{

	void AssertMarshalMethodData (MarshalMethodEntry entry, string? callbackField, string? connector, string? declaringType,
		string? implementedMethod, string? jniMethodName, string? jniMethodSignature, string? jniTypeName,
		string? nativeCallback, string? registeredMethod)
	{
		Assert.AreEqual (callbackField, entry.CallbackField?.ToString (), "Callback field should be the same.");
		Assert.AreEqual (connector, entry.Connector?.ToString (), "Connector should be the same.");
		Assert.AreEqual (declaringType, entry.DeclaringType.ToString (), "Declaring type should be the same.");
		Assert.AreEqual (implementedMethod, entry.ImplementedMethod?.ToString (), "Implemented method should be the same.");
		Assert.AreEqual (jniMethodName, entry.JniMethodName, "JNI method name should be the same.");
		Assert.AreEqual (jniMethodSignature, entry.JniMethodSignature, "JNI method signature should be the same.");
		Assert.AreEqual (jniTypeName, entry.JniTypeName, "JNI type name should be the same.");
		Assert.AreEqual (nativeCallback, entry.NativeCallback.ToString (), "Native callback should be the same.");
		Assert.AreEqual (registeredMethod, entry.RegisteredMethod?.ToString (), "Registered method should be the same.");
	}

	void AssertRewrittenMethodData (ConvertedMarshalMethodEntry converted, MarshalMethodEntry entry)
	{
		// Things that are different between the two:
		Assert.IsNull (converted.CallbackField, "Callback field will be null.");
		Assert.IsNull (converted.Connector, "Connector will be null.");

		var nativeCallback = converted.NativeCallback?.ToString () ?? "";
		var paren = nativeCallback.IndexOf ('(');
		var convertedNativeCallback = nativeCallback.Substring (0, paren) + "_mm_wrapper" + nativeCallback.Substring (paren);
		Assert.AreEqual (convertedNativeCallback, converted.ConvertedNativeCallback?.ToString (), "ConvertedNativeCallback should be the same.");

		// Things that should be the same between the two:
		Assert.AreEqual (entry.DeclaringType.ToString (), converted.DeclaringType.ToString (), "Declaring type should be the same.");
		Assert.AreEqual (entry.ImplementedMethod?.ToString (), converted.ImplementedMethod?.ToString (), "Implemented method should be the same.");
		Assert.AreEqual (entry.JniMethodName, converted.JniMethodName, "JNI method name should be the same.");
		Assert.AreEqual (entry.JniMethodSignature, converted.JniMethodSignature, "JNI method signature should be the same.");
		Assert.AreEqual (entry.JniTypeName, converted.JniTypeName, "JNI type name should be the same.");
		Assert.AreEqual (entry.NativeCallback.ToString (), converted.NativeCallback?.ToString (), "Native callback should be the same.");
		Assert.AreEqual (entry.RegisteredMethod?.ToString (), converted.RegisteredMethod?.ToString (), "Registered method should be the same.");
	}

	static ITaskItem CreateItem (string itemSpec, string abi)
	{
		var item = new TaskItem (itemSpec);
		item.SetMetadata ("Abi", abi);
		return item;
	}
}
