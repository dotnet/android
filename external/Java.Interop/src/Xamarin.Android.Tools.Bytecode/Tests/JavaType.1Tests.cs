using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaType_1Tests : ClassFileFixture {

		const string JavaType = "JavaType$1";

		[Test]
		public void ClassFileDescription ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 47,
				AccessFlags         = ClassAccessFlags.Super,
				FullName            = "com/xamarin/JavaType$1",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				InnerClasses = {
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$1",
						OuterClassName  = null,
						InnerName       = null,
						AccessFlags     = 0,
					},
				},
				Interfaces = {
					new TypeInfo ("java/lang/Runnable"),
				},
				Fields = {
					new ExpectedFieldDeclaration {
						Name                = "this$0",
						Descriptor          = "Lcom/xamarin/JavaType;",
						AccessFlags         = FieldAccessFlags.Final | FieldAccessFlags.Synthetic,
					},
				},
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = 0,
						ReturnDescriptor        = "V",
					},
					new ExpectedMethodDeclaration {
						Name                    = "run",
						AccessFlags             = MethodAccessFlags.Public,
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
