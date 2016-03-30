using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class NonGenericGlobalTypeTests : ClassFileFixture {

		const string JavaType = "NonGenericGlobalType";

		[Test]
		public void ClassFile_WithNonGenericGlobalType_class ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
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

		[Test]
		public void XmlDeclaration_WithNonGenericGlobalType_class ()
		{
			AssertXmlDeclaration (JavaType + ".class", JavaType + ".xml");
		}
	}
}

