using System.Text;
using System.Xml;
using Java.Interop.Tools.JavaTypeSystem;
using MonoDroid.Generation;
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
			var classParseXml = KotlinInlineClassEndToEndTests.BuildApiXmlFromKotlinFixture ("DefaultArguments.class", "DefaultArgumentsKt.class", "DefaultArgumentsInterface.class");

			StringAssert.Contains ("kotlin-default-jni-signature=\"(Lxat/bytecode/tests/DefaultArguments;Ljava/lang/String;IZILjava/lang/Object;)Ljava/lang/String;\"", classParseXml);
			StringAssert.Contains ("kotlin-default-jni-signature=\"(Ljava/lang/String;IILkotlin/jvm/internal/DefaultConstructorMarker;)V\"", classParseXml);

			// Like CodeGenerator, round-trip class-parse output through Java.Interop.Tools.JavaTypeSystem,
			// which removes the synthetic `$default` helpers and must keep the kotlin-default-* attributes.
			var sb = new StringBuilder ();
			using (var writer = XmlWriter.Create (sb))
				JavaXmlApiExporter.Save (JavaXmlApiImporter.ParseString (classParseXml), writer);
			var apiXml = sb.ToString ();

			var output = KotlinInlineClassEndToEndTests.GenerateCSharp (apiXml, out _, new CodeGenerationOptions { SupportDefaultInterfaceMethods = true });

			// fun greet (greeting: String, times: Int = 2, loud: Boolean = false)
			StringAssert.Contains ("public string Greet (string greeting, int times, bool loud)", output);
			StringAssert.Contains ("public string Greet (string greeting, int times)", output);
			StringAssert.Contains ("public string Greet (string greeting)", output);
			StringAssert.Contains ("const string __id = \"greet$default.(Lxat/bytecode/tests/DefaultArguments;Ljava/lang/String;IZILjava/lang/Object;)Ljava/lang/String;\";", output);
			StringAssert.Contains ("const int __mask0 = 4;", output);
			StringAssert.Contains ("const int __mask0 = 6;", output);
			StringAssert.Contains ("var loud = default (bool);", output);
			StringAssert.Contains ("var __self = this;", output);

			// The synthetic helpers themselves are not bound.
			StringAssert.DoesNotContain ("_default (", output);

			// fun middle (first: Int = 1, second: Int): only trailing defaults can be omitted.
			StringAssert.DoesNotContain ("Middle (int second)", output);

			// constructor (name: String, count: Int = 1)
			StringAssert.Contains ("public DefaultArguments (string name) :", output);
			StringAssert.Contains ("const string __id = \"(Ljava/lang/String;IILkotlin/jvm/internal/DefaultConstructorMarker;)V\";", output);

			// Top-level fun createDefaultArguments (name: String = "bob")
			StringAssert.Contains ("public static Xat.Bytecode.Tests.DefaultArguments CreateDefaultArguments ()", output);
			StringAssert.Contains ("const string __id = \"createDefaultArguments$default.(Ljava/lang/String;ILjava/lang/Object;)Lxat/bytecode/tests/DefaultArguments;\";", output);

			// interface: fun send (message: String, retries: Int = 3)
			StringAssert.Contains ("void Send (string message)", output);
			StringAssert.Contains ("static readonly JniPeerMembers _members_kotlin_default_xat_bytecode_tests_DefaultArgumentsInterface = new JniPeerMembers (\"xat/bytecode/tests/DefaultArgumentsInterface\", typeof (IDefaultArgumentsInterface), isInterface: true);", output);
			StringAssert.Contains ("_members_kotlin_default_xat_bytecode_tests_DefaultArgumentsInterface.StaticMethods.InvokeVoidMethod (__id, __args);", output);
		}
	}
}
