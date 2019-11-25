using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaType_ASCTests : ClassFileFixture {

		const string JavaType = "JavaType$ASC";

		[Test]
		public void ClassFileDescription ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 23,
				Deprecated          = true,
				AccessFlags         = ClassAccessFlags.Super,
				FullName            = "com/xamarin/JavaType$ASC",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				InnerClasses = {
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$ASC",
						OuterClassName  = "com/xamarin/JavaType",
						InnerName       = "ASC",
						AccessFlags     = ClassAccessFlags.Static,
					},
				},
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
		public void XmlDescription ()
		{
			AssertXmlDeclaration (JavaType + ".class", JavaType + ".xml");
		}
	}
}
