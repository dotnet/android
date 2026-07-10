using Mono.Cecil;
using MonoDroid.Generation;
using NUnit.Framework;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Xamarin.Android.Tools.ApiXmlAdjuster;

namespace generatortests
{
	//For now this is a basic smoke test to validate the `generator --only-xml-adjuster` option
	//	In the future, it should be expanded to fully test the Adjuster class
	[TestFixture]
	public class AdjusterTests
	{
		Adjuster adjuster;
		CodeGenerationOptions options;
		string inputFile, outputFile, temporaryAssembly;
		ModuleDefinition module;

		[SetUp]
		public void SetUp ()
		{
			temporaryAssembly = Path.GetTempFileName ();
			File.Copy (GetType ().Assembly.Location, temporaryAssembly, true);
			module = ModuleDefinition.ReadModule (temporaryAssembly);

			adjuster = new Adjuster ();
			options = new CodeGenerationOptions ();
			inputFile = Path.GetTempFileName ();
			outputFile = inputFile + ".adjusted";

			//We should fail on warnings & errors, and just log debug
			Log.LogDebugAction = TestContext.Out.WriteLine;
			Log.LogWarningAction =
				Log.LogErrorAction = Assert.Fail;

			File.WriteAllText (inputFile, @"
<api>
	<package name=""com.mypackage"">
		<class abstract=""false"" deprecated=""not deprecated"" extends=""java.lang.Object"" extends-generic-aware=""java.lang.Object"" final=""false"" name=""foo"" static=""false"" visibility=""public"">
			<constructor deprecated=""not deprecated"" final=""false"" name=""foo"" static=""false"" visibility=""public"" />
			<method abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""bar"" native=""false"" return=""void"" static=""false"" synchronized=""false"" visibility=""public"" />
		</class>
	</package>
</api>");

			foreach (var type in module.Types.Where (t => t.IsClass && t.Namespace == "Java.Lang")) {
				//Make sure we use this method instead of SymbolTable directly, to match what happens in generator.exe
				Xamarin.Android.Binder.CodeGenerator.ProcessReferencedType (type, options);
			}
		}

		[TearDown]
		public void TearDown ()
		{
			//HACK: put the Log callbacks back the way they were
			Log.LogDebugAction = null;
			Log.LogWarningAction = null;
			Log.LogErrorAction = null;

			module.Dispose ();
			File.Delete (temporaryAssembly);
			File.Delete (inputFile);
			File.Delete (outputFile);
		}

		[Test]
		public void Process ()
		{
			adjuster.Process (inputFile, options, options.SymbolTable.AllRegisteredSymbols (options).OfType<GenBase> ().ToArray (), outputFile, (int)Log.LoggingLevel.Debug);

			FileAssert.Exists (outputFile);
		}

		[Test]
		public void AdjustNotNullAnnotations ()
		{
			var input = @"
<api>
  <package name=""com.mypackage"">
    <class abstract=""false"" deprecated=""not deprecated"" jni-extends=""Ljava/lang/Object;"" extends=""java.lang.Object"" extends-generic-aware=""java.lang.Object"" final=""false"" name=""NotNullClass"" jni-signature=""Lcom/xamarin/NotNullClass;"" source-file-name=""NotNullClass.java"" static=""false"" visibility=""public"">
      <constructor deprecated=""not deprecated"" final=""false"" name=""NotNullClass"" static=""false"" visibility=""public"" bridge=""false"" synthetic=""false"" jni-signature=""()V"" />
      <method abstract=""false"" deprecated=""not deprecated"" final=""false"" name=""notNullFunc"" native=""false"" return=""void"" jni-return=""V"" static=""false"" synchronized=""false"" visibility=""public"" bridge=""false"" synthetic=""false"" jni-signature=""(Ljava/lang/String;)V"" return-not-null=""true"">
        <parameter name=""value"" type=""java.lang.String"" jni-type=""Ljava/lang/String;"" not-null=""true"" />
      </method>
      <field deprecated=""not deprecated"" final=""false"" name=""notNullField"" static=""false"" synthetic=""false"" transient=""false"" type=""java.lang.String"" type-generic-aware=""java.lang.String"" jni-signature=""Ljava/lang/String;"" not-null=""true"" visibility=""public"" volatile=""false"" />
    </class>
  </package>
</api>";

			var api = new JavaApi ();
			api.LoadReferences (options, options.SymbolTable.AllRegisteredSymbols (options).OfType<GenBase> ().ToArray ());

			using (var sr = new StringReader (input))
			using (var xml = XmlReader.Create (sr))
				api.Load (xml, false);

			api.StripNonBindables ();
			api.Resolve ();
			api.CreateGenericInheritanceMapping ();
			api.MarkOverrides ();
			api.FindDefects ();

			using (var sb = new StringWriter ()) {
				using (var writer = XmlWriter.Create (sb))
					api.Save (writer);

				var root = XElement.Parse (sb.ToString ());

				Assert.AreEqual ("true", root.Element ("package").Element ("class").Element ("method").Attribute ("return-not-null").Value);
				Assert.AreEqual ("true", root.Element ("package").Element ("class").Element ("method").Element ("parameter").Attribute ("not-null").Value);
				Assert.AreEqual ("true", root.Element ("package").Element ("class").Element ("field").Attribute ("not-null").Value);
			}
		}
	}
}
