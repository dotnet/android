using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaType_PSCTests : ClassFileFixture {

		const string JavaType = "JavaType$PSC";

		[Test]
		public void ClassFileDescription ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 20,
				AccessFlags         = ClassAccessFlags.Public | ClassAccessFlags.Super | ClassAccessFlags.Abstract,
				FullName            = "com/xamarin/JavaType$PSC",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				TypeParameters = {
					new TypeParameterInfo {
						Identifier  = "TString",
						InterfaceBounds = {
							"Ljava/lang/CharSequence;",
							"Ljava/lang/Appendable;",
						},
					},
					new TypeParameterInfo {
						Identifier  = "TStringList",
						ClassBound  = "Ljava/util/ArrayList<TTString;>;",
						InterfaceBounds = {
							"Ljava/util/List<TTString;>;",
						},
					},
					new TypeParameterInfo {
						Identifier  = "TReturn",
						ClassBound  = "Ljava/lang/Object;",
					},
				},
				InnerClasses = {
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$PSC",
						OuterClassName  = "com/xamarin/JavaType",
						InnerName       = "PSC",
						AccessFlags     = ClassAccessFlags.Public | ClassAccessFlags.Static | ClassAccessFlags.Abstract,
					},
				},
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
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
