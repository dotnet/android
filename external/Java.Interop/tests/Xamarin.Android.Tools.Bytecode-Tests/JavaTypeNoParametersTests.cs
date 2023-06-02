using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaTypeNoParametersTests : ClassFileFixture {

		const string JavaType = "JavaTypeNoParameters";

		[Test]
		public void ClassFile_WithNonGenericGlobalType_class ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x37,
				MinorVersion        = 0,
				ConstantPoolCount   = 18,
				AccessFlags         = ClassAccessFlags.Public | ClassAccessFlags.Super,
				FullName            = "com/xamarin/JavaTypeNoParameters",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							new ParameterInfo ("copy", "Lcom/xamarin/JavaTypeNoParameters;", "Lcom/xamarin/JavaTypeNoParameters;"),
						},
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

