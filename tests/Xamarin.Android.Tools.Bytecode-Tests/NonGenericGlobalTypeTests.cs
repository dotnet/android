using System;

using Xamarin.Android.Tools.Bytecode;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestClass]
	public class NonGenericGlobalTypeTests : ClassFileFixture {

		const string JavaType = "NonGenericGlobalType";

		[TestMethod]
		public void ClassFile_WithNonGenericGlobalType_class ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x37,
				MinorVersion        = 0,
				ConstantPoolCount   = 16,
				AccessFlags         = ClassAccessFlags.Super,
				FullName            = "NonGenericGlobalType",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = 0,
						ReturnDescriptor        = "V",
					},
				}
			}.Assert (c);
		}

		[TestMethod]
		public void XmlDeclaration_WithNonGenericGlobalType_class ()
		{
			AssertXmlDeclaration (JavaType + ".class", JavaType + ".xml");
		}
	}
}

