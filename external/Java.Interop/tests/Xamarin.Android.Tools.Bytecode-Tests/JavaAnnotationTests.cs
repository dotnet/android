using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaAnnotationTests : ClassFileFixture {

		const string JavaType = "JavaAnnotation";

		[Test]
		public void ClassFile_WithJavaAnnotation_class ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 23,
				AccessFlags         = ClassAccessFlags.Public | ClassAccessFlags.Interface | ClassAccessFlags.Abstract | ClassAccessFlags.Annotation,
				FullName            = "com/xamarin/JavaAnnotation",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				Interfaces = {
					new TypeInfo ("java/lang/annotation/Annotation"),
				},
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "value",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Abstract,
						ReturnDescriptor        = "[Ljava/lang/String;",
					},
				}
			}.Assert (c);
		}

		[Test]
		public void XmlDeclaration_WithJavaAnnotation_class ()
		{
			AssertXmlDeclaration (JavaType + ".class", JavaType + ".xml");
		}
	}
}

