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
	[Test]
	public void MarshalMethodsCollectionScanning ()
	{
		// This test does 2 things:
		// - Builds a binding project in Debug mode to create an assembly that contains convertible
		//   marshal methods to ensure they are found by MarshalMethodsCollection
		// - Builds the same project in Release mode which rewrites the assembly to ensure those
		//   same marshal methods can be found after they are rewritten
		var proj = new XamarinAndroidApplicationProject {
			ProjectName = "mmtest",
		};

		proj.Sources.Add (new AndroidItem.AndroidLibrary ("javaclasses.jar") {
			BinaryContent = () => ResourceData.JavaSourceJarTestJar,
		});

		proj.AndroidJavaSources.Add (new AndroidItem.AndroidJavaSource ("JavaSourceTestInterface.java") {
			Encoding = Encoding.ASCII,
			TextContent = () => ResourceData.JavaSourceTestInterface,
			Metadata = { { "Bind", "True" } },
		});

		proj.AndroidJavaSources.Add (new AndroidItem.AndroidJavaSource ("JavaSourceTestExtension.java") {
			Encoding = Encoding.ASCII,
			TextContent = () => ResourceData.JavaSourceTestExtension,
			Metadata = { { "Bind", "True" } },
		});

		proj.MainActivity = proj.DefaultMainActivity.Replace ("//${AFTER_MAINACTIVITY}", """
			// Implements Java interface method
			class MyGreeter : Java.Lang.Object, Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterface {
			    public virtual string? GreetWithQuestion (string? p0, Java.Util.Date? p1, string? p2) => "greetings!";
			}

			// Overrides implemented Java interface method
			class MyExtendedGreeter : MyGreeter {
			    public override string? GreetWithQuestion (string? p0, Java.Util.Date? p1, string? p2) => "more greetings!";
			}

			// Implements Java interface method (duplicate)
			class MyGreeter2 : Java.Lang.Object, Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterface {
			    public virtual string? GreetWithQuestion (string? p0, Java.Util.Date? p1, string? p2) => "duplicate greetings!";
			}
		
			// Overrides Java class method
			class MyOverriddenGreeter : Com.Xamarin.Android.Test.Msbuildtest.JavaSourceTestExtension {
			    public override string? GreetWithQuestion (string? p0, Java.Util.Date? p1, string? p2) => "even more greetings!";
			}
		""");

		var builder = CreateApkBuilder ();
		Assert.IsTrue (builder.Build (proj), "`dotnet build` should succeed");
		builder.AssertHasNoWarnings ();

		var intermediateDebugOutputPath = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "android", "assets", "arm64-v8a");
		var outputDebugDll = Path.Combine (intermediateDebugOutputPath, $"{proj.ProjectName}.dll");

		var log = new TaskLoggingHelper (new MockBuildEngine (TestContext.Out, [], [], []), nameof (MarshalMethodsCollectionScanning));
		var xaResolver = new XAAssemblyResolver (Tools.AndroidTargetArch.Arm64, log, false);
		xaResolver.SearchDirectories.Add (Path.GetDirectoryName (outputDebugDll)!);

		var collection = MarshalMethodsCollection.FromAssemblies (Tools.AndroidTargetArch.Arm64, [CreateItem (outputDebugDll, "arm64-v8a")], xaResolver, log);

		Assert.AreEqual (3, collection.MarshalMethods.Count);
		Assert.AreEqual (0, collection.ConvertedMarshalMethods.Count);

		var key1 = "Android.App.Activity, Mono.Android\tOnCreate";
		var key2 = "Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterface, mmtest\tGreetWithQuestion";
		var key3 = "Com.Xamarin.Android.Test.Msbuildtest.JavaSourceTestExtension, mmtest\tGreetWithQuestion";

		Assert.AreEqual (1, collection.MarshalMethods [key1].Count);
		Assert.AreEqual (2, collection.MarshalMethods [key2].Count);
		Assert.AreEqual (1, collection.MarshalMethods [key3].Count);

		AssertMarshalMethodData (collection.MarshalMethods [key1] [0],
			callbackField: null,
			connector: "System.Delegate Android.App.Activity::GetOnCreate_Landroid_os_Bundle_Handler()",
			declaringType: "mmtest.MainActivity",
			implementedMethod: "System.Void mmtest.MainActivity::OnCreate(Android.OS.Bundle)",
			jniMethodName: "onCreate",
			jniMethodSignature: "(Landroid/os/Bundle;)V",
			jniTypeName: "com/xamarin/marshalmethodscollectionscanning/MainActivity",
			nativeCallback: "System.Void Android.App.Activity::n_OnCreate_Landroid_os_Bundle_(System.IntPtr,System.IntPtr,System.IntPtr)",
			registeredMethod: "System.Void Android.App.Activity::OnCreate(Android.OS.Bundle)");

		AssertMarshalMethodData (collection.MarshalMethods [key2] [0],
			callbackField: null,
			connector: "System.Delegate Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterfaceInvoker::GetGreetWithQuestion_Ljava_lang_String_Ljava_util_Date_Ljava_lang_String_Handler()",
			declaringType: "mmtest.MyGreeter",
			implementedMethod: "System.String Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterface::GreetWithQuestion(System.String,Java.Util.Date,System.String)",
			jniMethodName: "greetWithQuestion",
			jniMethodSignature: "(Ljava/lang/String;Ljava/util/Date;Ljava/lang/String;)Ljava/lang/String;",
			jniTypeName: "crc644a923d2fc5ca7023/MyGreeter",
			nativeCallback: "System.IntPtr Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterfaceInvoker::n_GreetWithQuestion_Ljava_lang_String_Ljava_util_Date_Ljava_lang_String_(System.IntPtr,System.IntPtr,System.IntPtr,System.IntPtr,System.IntPtr)",
			registeredMethod: "System.String Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterface::GreetWithQuestion(System.String,Java.Util.Date,System.String)");

		AssertMarshalMethodData (collection.MarshalMethods [key2] [1],
			callbackField: null,
			connector: "System.Delegate Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterfaceInvoker::GetGreetWithQuestion_Ljava_lang_String_Ljava_util_Date_Ljava_lang_String_Handler()",
			declaringType: "mmtest.MyGreeter2",
			implementedMethod: "System.String Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterface::GreetWithQuestion(System.String,Java.Util.Date,System.String)",
			jniMethodName: "greetWithQuestion",
			jniMethodSignature: "(Ljava/lang/String;Ljava/util/Date;Ljava/lang/String;)Ljava/lang/String;",
			jniTypeName: "crc644a923d2fc5ca7023/MyGreeter2",
			nativeCallback: "System.IntPtr Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterfaceInvoker::n_GreetWithQuestion_Ljava_lang_String_Ljava_util_Date_Ljava_lang_String_(System.IntPtr,System.IntPtr,System.IntPtr,System.IntPtr,System.IntPtr)",
			registeredMethod: "System.String Com.Xamarin.Android.Test.Msbuildtest.IJavaSourceTestInterface::GreetWithQuestion(System.String,Java.Util.Date,System.String)");

		AssertMarshalMethodData (collection.MarshalMethods [key3] [0],
			callbackField: null,
			connector: "System.Delegate Com.Xamarin.Android.Test.Msbuildtest.JavaSourceTestExtension::GetGreetWithQuestion_Ljava_lang_String_Ljava_util_Date_Ljava_lang_String_Handler()",
			declaringType: "mmtest.MyOverriddenGreeter",
			implementedMethod: "System.String mmtest.MyOverriddenGreeter::GreetWithQuestion(System.String,Java.Util.Date,System.String)",
			jniMethodName: "greetWithQuestion",
			jniMethodSignature: "(Ljava/lang/String;Ljava/util/Date;Ljava/lang/String;)Ljava/lang/String;",
			jniTypeName: "crc644a923d2fc5ca7023/MyOverriddenGreeter",
			nativeCallback: "System.IntPtr Com.Xamarin.Android.Test.Msbuildtest.JavaSourceTestExtension::n_GreetWithQuestion_Ljava_lang_String_Ljava_util_Date_Ljava_lang_String_(System.IntPtr,System.IntPtr,System.IntPtr,System.IntPtr,System.IntPtr)",
			registeredMethod: "System.String Com.Xamarin.Android.Test.Msbuildtest.JavaSourceTestExtension::GreetWithQuestion(System.String,Java.Util.Date,System.String)");

		// Recompile with Release so marshal methods get rewritten
		proj.IsRelease = true;

		Assert.IsTrue (builder.Build (proj), "`dotnet build` should succeed");
		builder.AssertHasNoWarnings ();

		// Rescan for modified marshal methods
		var intermediateReleaseOutputPath = Path.Combine (Root, builder.ProjectDirectory, proj.IntermediateOutputPath, "android-arm64", "linked");
		var outputReleaseDll = Path.Combine (intermediateReleaseOutputPath, $"{proj.ProjectName}.dll");

		xaResolver = new XAAssemblyResolver (Tools.AndroidTargetArch.Arm64, log, false);
		xaResolver.SearchDirectories.Add (Path.GetDirectoryName (outputReleaseDll)!);

		var releaseCollection = MarshalMethodsCollection.FromAssemblies (Tools.AndroidTargetArch.Arm64, [CreateItem (outputReleaseDll, "arm64-v8a")], xaResolver, log);

		Assert.AreEqual (0, releaseCollection.MarshalMethods.Count);
		Assert.AreEqual (3, releaseCollection.ConvertedMarshalMethods.Count);

		AssertRewrittenMethodData (releaseCollection.ConvertedMarshalMethods [key1] [0], collection.MarshalMethods [key1] [0]);
		AssertRewrittenMethodData (releaseCollection.ConvertedMarshalMethods [key2] [0], collection.MarshalMethods [key2] [0]);
		AssertRewrittenMethodData (releaseCollection.ConvertedMarshalMethods [key2] [1], collection.MarshalMethods [key2] [1]);
		AssertRewrittenMethodData (releaseCollection.ConvertedMarshalMethods [key3] [0], collection.MarshalMethods [key3] [0]);
	}

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
