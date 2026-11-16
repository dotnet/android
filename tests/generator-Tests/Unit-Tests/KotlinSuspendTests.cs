using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NUnit.Framework;
using Xamarin.Android.Tools.Bytecode;

namespace generatortests
{
	// Drives the real Kotlin `SuspendFunctions` class (compiled by
	// tests/Xamarin.Android.Tools.Bytecode-Tests/kotlin-gradle/) through
	// class-parse and the generator, and checks the `Task` wrappers.
	[TestFixture]
	public class KotlinSuspendTests
	{
		const string Invoke = "global::Android.Runtime.KotlinCoroutines.InvokeAsync";
		const string Continuation = "global::Android.Runtime.Extensions.JavaCast<Kotlin.Coroutines.IContinuation> (__continuation)";
		const string Token = "global::System.Threading.CancellationToken cancellationToken = default";

		[Test]
		public void GeneratesTaskWrappersForSuspendFunctions ()
		{
			var output = KotlinInlineClassEndToEndTests.GenerateCSharp (BuildApiXml (), out _);

			// The bound method that takes the Continuation is unchanged.
			StringAssert.Contains ("public Java.Lang.Object GetInt (int value, Kotlin.Coroutines.IContinuation _completion)", output);

			StringAssert.Contains ($"public global::System.Threading.Tasks.Task<int> GetIntAsync (int value, {Token})", output);
			StringAssert.Contains ($"return {Invoke}<int> (__continuation => GetInt (value, {Continuation}), cancellationToken);", output);

			StringAssert.Contains ($"public global::System.Threading.Tasks.Task<string> GetNullableStringAsync ({Token})", output);
			StringAssert.Contains ($"return {Invoke}<string> (__continuation => GetNullableString ({Continuation}), cancellationToken);", output);

			StringAssert.Contains ($"public global::System.Threading.Tasks.Task DoWorkAsync ({Token})", output);
			StringAssert.Contains ($"return {Invoke} (__continuation => DoWork ({Continuation}), cancellationToken);", output);

			// A type parameter result is not inferred.
			StringAssert.Contains ("public global::System.Threading.Tasks.Task<global::Java.Lang.Object> EchoAsync (Java.Lang.Object value, ", output);

			StringAssert.DoesNotContain ("NotSuspendAsync", output);
		}

		static string BuildApiXml ()
		{
			var classes = new List<ClassFile> { KotlinInlineClassEndToEndTests.LoadClassFile ("SuspendFunctions.class") };
			KotlinFixups.Fixup (classes);

			var classPath = new ClassPath { ApiSource = "class-parse" };
			foreach (var c in classes)
				classPath.Add (c);

			var sw = new StringWriter ();
			classPath.SaveXmlDescription (sw);

			var doc = XDocument.Parse (sw.ToString ());
			doc.Root.AddFirst (XElement.Parse (
				"<package name='java.lang' jni-name='java/lang'>" +
				"<class abstract='false' deprecated='not deprecated' final='false' name='Object' static='false' visibility='public' jni-signature='Ljava/lang/Object;' />" +
				"</package>"));
			doc.Root.AddFirst (XElement.Parse (
				"<package name='kotlin.coroutines' jni-name='kotlin/coroutines'>" +
				"<interface abstract='true' deprecated='not deprecated' final='false' name='Continuation' static='false' visibility='public' jni-signature='Lkotlin/coroutines/Continuation;' />" +
				"</package>"));
			return doc.ToString ();
		}
	}
}
