using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace generatortests
{
	// Drives real Kotlin .class files (compiled from
	// tests/Xamarin.Android.Tools.Bytecode-Tests/kotlin-gradle/src/main/kotlin/DefaultArguments.kt)
	// through class-parse, the api.xml adjuster and the generator.
	[TestClassAttribute]
	public class KotlinDefaultArgumentsTests
	{
		[TestMethodAttribute]
		public void GeneratesOverloadsForTrailingDefaultArguments ()
		{
			// The helper runs class-parse output through the JavaTypeSystem adjuster, like CodeGenerator,
			// which removes the synthetic $default helpers and must keep the kotlin-default-* attributes.
			var apiXml = KotlinInlineClassEndToEndTests.BuildApiXmlFromKotlinFixture ("DefaultArguments.class", "DefaultArgumentsKt.class", "DefaultArgumentsInterface.class", "DefaultArgumentsChild.class");

			Assert.Contains ("kotlin-default-jni-signature=\"(Lxat/bytecode/tests/DefaultArguments;Ljava/lang/String;IZILjava/lang/Object;)Ljava/lang/String;\"", apiXml);
			Assert.Contains ("kotlin-default-jni-signature=\"(Ljava/lang/String;IILkotlin/jvm/internal/DefaultConstructorMarker;)V\"", apiXml);

			var output = KotlinInlineClassEndToEndTests.GenerateCSharp (apiXml, defaultInterfaceMethods: true).Output;

			// fun greet (greeting: String, times: Int = 2, loud: Boolean = false)
			Assert.Contains ("public string Greet (string greeting, int times)", output);
			Assert.Contains ("public string Greet (string greeting)", output);
			Assert.Contains ("const string __id = \"greet$default.(Lxat/bytecode/tests/DefaultArguments;Ljava/lang/String;IZILjava/lang/Object;)Ljava/lang/String;\";", output);
			Assert.Contains ("const int __mask0 = 4;", output);
			Assert.Contains ("const int __mask0 = 6;", output);

			// The synthetic helpers themselves are not bound.
			Assert.DoesNotContain ("_default (", output);

			// fun middle (first: Int = 1, second: Int): only trailing defaults can be omitted.
			Assert.DoesNotContain ("Middle (int second)", output);

			// fun toString (indent: Int = 0): the overload hides object.ToString ().
			Assert.Contains ("public new string ToString ()", output);

			// fun format (): CharSequence already has a string Format () overload.
			Assert.AreEqual (1, Regex.Matches (output, @"public string Format \(\)").Count);

			// fun log (vararg items: String) is Log (params string[] items).
			Assert.DoesNotContain ("public void Log (string[] items)", output);

			// constructor (name: String, count: Int = 1)
			Assert.Contains ("public DefaultArguments (string name) :", output);
			Assert.Contains ("const string __id = \"(Ljava/lang/String;IILkotlin/jvm/internal/DefaultConstructorMarker;)V\";", output);

			// Top-level fun createDefaultArguments (name: String = "bob")
			Assert.Contains ("public static Xat.Bytecode.Tests.DefaultArguments CreateDefaultArguments ()", output);
			Assert.Contains ("const string __id = \"createDefaultArguments$default.(Ljava/lang/String;ILjava/lang/Object;)Lxat/bytecode/tests/DefaultArguments;\";", output);

			// interface: fun send (message: String, retries: Int = 3)
			Assert.Contains ("void Send (string message)", output);

			// DefaultArgumentsChild does not hide the inherited Send (string, int) and Send (string).
			Assert.Contains ("void Send (string message, int retries, bool urgent);", output);
			Assert.Contains ("void Send (string message, bool urgent);", output);
			Assert.DoesNotContain ("send$default.(Lxat/bytecode/tests/DefaultArgumentsChild;", output);
		}
	}
}
