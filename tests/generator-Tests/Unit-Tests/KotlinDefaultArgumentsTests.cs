using NUnit.Framework;

namespace generatortests
{
	// Drives real Kotlin .class files (compiled from
	// tests/Xamarin.Android.Tools.Bytecode-Tests/kotlin-gradle/src/main/kotlin/DefaultArguments.kt)
	// through class-parse, the api.xml adjuster and the generator.
	[TestFixture]
	public class KotlinDefaultArgumentsTests
	{
		[Test]
		public void GeneratesOverloadsForTrailingDefaultArguments ()
		{
			// The helper runs class-parse output through the JavaTypeSystem adjuster, like CodeGenerator,
			// which removes the synthetic $default helpers and must keep the kotlin-default-* attributes.
			var apiXml = KotlinInlineClassEndToEndTests.BuildApiXmlFromKotlinFixture ("DefaultArguments.class", "DefaultArgumentsKt.class", "DefaultArgumentsInterface.class", "DefaultArgumentsChild.class");

			StringAssert.Contains ("kotlin-default-jni-signature=\"(Lxat/bytecode/tests/DefaultArguments;Ljava/lang/String;IZILjava/lang/Object;)Ljava/lang/String;\"", apiXml);
			StringAssert.Contains ("kotlin-default-jni-signature=\"(Ljava/lang/String;IILkotlin/jvm/internal/DefaultConstructorMarker;)V\"", apiXml);

			var output = KotlinInlineClassEndToEndTests.GenerateCSharp (apiXml, out _, defaultInterfaceMethods: true);

			// fun greet (greeting: String, times: Int = 2, loud: Boolean = false)
			StringAssert.Contains ("public string Greet (string greeting, int times)", output);
			StringAssert.Contains ("public string Greet (string greeting)", output);
			StringAssert.Contains ("const string __id = \"greet$default.(Lxat/bytecode/tests/DefaultArguments;Ljava/lang/String;IZILjava/lang/Object;)Ljava/lang/String;\";", output);
			StringAssert.Contains ("const int __mask0 = 4;", output);
			StringAssert.Contains ("const int __mask0 = 6;", output);

			// The synthetic helpers themselves are not bound.
			StringAssert.DoesNotContain ("_default (", output);

			// fun middle (first: Int = 1, second: Int): only trailing defaults can be omitted.
			StringAssert.DoesNotContain ("Middle (int second)", output);

			// fun toString (indent: Int = 0): the overload hides object.ToString ().
			StringAssert.Contains ("public new string ToString ()", output);

			// constructor (name: String, count: Int = 1)
			StringAssert.Contains ("public DefaultArguments (string name) :", output);
			StringAssert.Contains ("const string __id = \"(Ljava/lang/String;IILkotlin/jvm/internal/DefaultConstructorMarker;)V\";", output);

			// Top-level fun createDefaultArguments (name: String = "bob")
			StringAssert.Contains ("public static Xat.Bytecode.Tests.DefaultArguments CreateDefaultArguments ()", output);
			StringAssert.Contains ("const string __id = \"createDefaultArguments$default.(Ljava/lang/String;ILjava/lang/Object;)Lxat/bytecode/tests/DefaultArguments;\";", output);

			// interface: fun send (message: String, retries: Int = 3)
			StringAssert.Contains ("void Send (string message)", output);

			// interface DefaultArgumentsChild : DefaultArgumentsInterface { fun send (message: String, retries: Int, urgent: Boolean = false) }
			// does not hide the inherited Send (string, int).
			StringAssert.Contains ("void Send (string message, int retries, bool urgent);", output);
			StringAssert.DoesNotContain ("send$default.(Lxat/bytecode/tests/DefaultArgumentsChild;", output);
			StringAssert.Contains ("_members_kotlin_default_xat_bytecode_tests_DefaultArgumentsInterface.StaticMethods.InvokeVoidMethod (__id, __args);", output);
		}
	}
}
