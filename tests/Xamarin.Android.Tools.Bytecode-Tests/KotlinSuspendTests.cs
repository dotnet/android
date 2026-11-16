using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Xamarin.Android.Tools.Bytecode;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestFixture]
	public class KotlinSuspendTests : ClassFileFixture
	{
		[Test]
		public void Fixup_SetsKotlinSuspendReturnType ()
		{
			var klass = LoadClassFile ("SuspendFunctions.class");
			KotlinFixups.Fixup (new List<ClassFile> { klass });

			string GetReturnType (string name) => klass.Methods.Single (m => m.Name == name).KotlinSuspendReturnType;

			Assert.AreEqual ("kotlin/Int", GetReturnType ("getInt"));
			Assert.AreEqual ("kotlin/String?", GetReturnType ("getNullableString"));
			Assert.AreEqual ("kotlin/Unit", GetReturnType ("doWork"));
			Assert.AreEqual ("kotlin/Any?", GetReturnType ("echo"));
			Assert.IsNull (GetReturnType ("notSuspend"));
		}

		[Test]
		public void XmlOutput_ContainsKotlinSuspendReturnType ()
		{
			var klass = LoadClassFile ("SuspendFunctions.class");
			KotlinFixups.Fixup (new List<ClassFile> { klass });

			var xml = new XmlClassDeclarationBuilder (klass).ToXElement ();
			var method = xml.Elements ("method").Single (m => (string) m.Attribute ("name") == "getInt");

			Assert.AreEqual ("kotlin/Int", (string) method.Attribute ("kotlin-suspend-return-type"));
		}
	}
}
