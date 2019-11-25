using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaType_RNC_RPNCTests : ClassFileFixture {

		const string JavaType = "JavaType$RNC$RPNC";

		[Test]
		public void ClassFileDescription ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 46,
				AccessFlags         = ClassAccessFlags.Public | ClassAccessFlags.Super | ClassAccessFlags.Abstract,
				FullName            = "com/xamarin/JavaType$RNC$RPNC",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				TypeParameters = {
					new TypeParameterInfo {
						Identifier  = "E3",
						ClassBound  = "Ljava/lang/Object;",
					},
				},
				InnerClasses = {
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$RNC",
						OuterClassName  = "com/xamarin/JavaType",
						InnerName       = "RNC",
						AccessFlags     = ClassAccessFlags.Protected | ClassAccessFlags.Abstract,
					},
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$RNC$RPNC",
						OuterClassName  = "com/xamarin/JavaType$RNC",
						InnerName       = "RPNC",
						AccessFlags     = ClassAccessFlags.Public | ClassAccessFlags.Abstract,
					},
				},
				Fields = {
					new ExpectedFieldDeclaration {
						Name                = "this$1",
						Descriptor          = "Lcom/xamarin/JavaType$RNC;",
						AccessFlags         = FieldAccessFlags.Final | FieldAccessFlags.Synthetic,
					},
				},
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							new ParameterInfo ("value1",    "Ljava/lang/Object;", "TE;"),
							new ParameterInfo ("value2",    "Ljava/lang/Object;", "TE2;"),
							new ParameterInfo ("value3",    "Ljava/lang/Object;", "TE3;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "fromE2",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Abstract,
						ReturnDescriptor        = "Ljava/lang/Object;",
						ReturnGenericDescriptor = "TE3;",
						Parameters = {
							new ParameterInfo ("value",     "Ljava/lang/Object;", "TE2;"),
						},
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
