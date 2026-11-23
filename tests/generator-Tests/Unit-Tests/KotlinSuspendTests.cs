using System.Xml.Linq;
using NUnit.Framework;

namespace generatortests
{
	// Drives the real Kotlin `SuspendFunctions` class through class-parse, the adjuster and the generator.
	[TestFixture]
	public class KotlinSuspendTests
	{
		const string Call = "global::Android.Runtime.KotlinCoroutines.CallSuspendFunctionAsync";
		const string CallNullable = "global::Android.Runtime.KotlinCoroutines.CallNullableSuspendFunctionAsync";
		const string Continuation = "global::Android.Runtime.Extensions.JavaCast<Kotlin.Coroutines.IContinuation> (__continuation)";
		const string Token = "global::System.Threading.CancellationToken cancellationToken = default";

		[Test]
		public void GeneratesTaskWrappersForSuspendFunctions ()
		{
			// In a real build, `Continuation` comes from the Kotlin.StdLib reference assembly.
			var api = XDocument.Parse (KotlinInlineClassEndToEndTests.BuildApiXmlFromKotlinFixture ("SuspendFunctions.class"));
			api.Root.AddFirst (XElement.Parse (
				"<package name='kotlin.coroutines' jni-name='kotlin/coroutines'>" +
				"<interface abstract='true' deprecated='not deprecated' final='false' name='Continuation' static='false' visibility='public' jni-signature='Lkotlin/coroutines/Continuation;' />" +
				"</package>"));
			var output = KotlinInlineClassEndToEndTests.GenerateCSharp (api.ToString (), out _);

			// The bound method that takes the Continuation is unchanged.
			StringAssert.Contains ("public Java.Lang.Object GetInt (int value, Kotlin.Coroutines.IContinuation _completion)", output);

			StringAssert.Contains ($"public global::System.Threading.Tasks.Task<int> GetIntAsync (int value, {Token})", output);
			StringAssert.Contains ($"return {Call}<int> (__continuation => GetInt (value, {Continuation}), cancellationToken);", output);

			StringAssert.Contains ($"public global::System.Threading.Tasks.Task<string> GetNullableStringAsync ({Token})", output);
			StringAssert.Contains ($"return {CallNullable}<string> (__continuation => GetNullableString ({Continuation}), cancellationToken);", output);

			StringAssert.Contains ($"public global::System.Threading.Tasks.Task<int?> GetNullableIntAsync ({Token})", output);
			StringAssert.Contains ($"return {CallNullable}<int?> (__continuation => GetNullableInt ({Continuation}), cancellationToken);", output);

			StringAssert.Contains ($"public global::System.Threading.Tasks.Task DoWorkAsync ({Token})", output);
			StringAssert.Contains ($"return {CallNullable}<global::Java.Lang.Object> (__continuation => DoWork ({Continuation}), cancellationToken);", output);

			// A type parameter result is not inferred.
			StringAssert.Contains ("public global::System.Threading.Tasks.Task<global::Java.Lang.Object> EchoAsync (Java.Lang.Object value, ", output);

			StringAssert.DoesNotContain ("NotSuspendAsync", output);
		}
	}
}
