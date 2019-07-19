using Mono.Cecil;
using MonoDroid.Generation;
using NUnit.Framework;
using System.IO;
using System.Linq;
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
			foreach (var type in module.Types.Where (t => t.IsClass && t.Namespace == "Java.Lang")) {
				//Make sure we use this method instead of SymbolTable directly, to match what happens in generator.exe
				Xamarin.Android.Binder.CodeGenerator.ProcessReferencedType (type, options);
			}

			adjuster.Process (inputFile, options, options.SymbolTable.AllRegisteredSymbols (options).OfType<GenBase> ().ToArray (), outputFile, (int)Log.LoggingLevel.Debug);

			FileAssert.Exists (outputFile);
		}
	}
}
