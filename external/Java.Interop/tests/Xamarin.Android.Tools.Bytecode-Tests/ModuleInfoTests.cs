using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class ModuleInfoTests : ClassFileFixture {

		const string JavaType = "module-info";

		[Test]
		public void ClassFile ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x37,
				MinorVersion        = 0,
				ConstantPoolCount   = 13,
				AccessFlags         = ClassAccessFlags.Module,
				FullName            = "module-info",
			}.Assert (c);

			Assert.AreEqual (2, c.Attributes.Count);

			Assert.AreEqual ("SourceFile",          c.Attributes [0].Name);
			var sourceFileAttr = c.Attributes [0] as SourceFileAttribute;
			Assert.IsTrue (sourceFileAttr != null);
			Assert.AreEqual ("module-info.java",    sourceFileAttr.FileName);

			Assert.AreEqual ("Module",              c.Attributes [1].Name);
			var moduleAttr = c.Attributes [1] as ModuleAttribute;
			Assert.IsTrue (moduleAttr != null);
			Assert.AreEqual ("com.xamarin",         moduleAttr.ModuleName);
			Assert.AreEqual (null,                  moduleAttr.ModuleVersion);
			Assert.AreEqual (1,                     moduleAttr.Requires.Count);
			Assert.AreEqual ("java.base",           moduleAttr.Requires [0].Requires);
			Assert.AreEqual (1,                     moduleAttr.Exports.Count);
			Assert.AreEqual ("com/xamarin",         moduleAttr.Exports [0].Exports);
		}
	}
}

