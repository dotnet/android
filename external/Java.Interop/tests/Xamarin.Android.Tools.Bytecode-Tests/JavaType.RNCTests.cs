using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaType_RNCTests : ClassFileFixture {

		const string JavaType = "JavaType$RNC";

		[Test]
		public void ClassFileDescription ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 44,
				AccessFlags         = ClassAccessFlags.Public | ClassAccessFlags.Super | ClassAccessFlags.Abstract,
				FullName            = "com/xamarin/JavaType$RNC",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				TypeParameters = {
					new TypeParameterInfo {
						Identifier  = "E2",
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
						Name                = "this$0",
						Descriptor          = "Lcom/xamarin/JavaType;",
						AccessFlags         = FieldAccessFlags.Final | FieldAccessFlags.Synthetic,
					},
				},
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Protected,
						ReturnDescriptor        = "V",
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Protected,
						ReturnDescriptor        = "V",
						Parameters = {
							new ParameterInfo ("value1",    "Ljava/lang/Object;",   "TE;"),
							new ParameterInfo ("value2",    "Ljava/lang/Object;",   "TE2;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "fromE",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Abstract,
						ReturnDescriptor        = "Ljava/lang/Object;",
						ReturnGenericDescriptor = "TE2;",
						Parameters = {
							new ParameterInfo ("value",     "Ljava/lang/Object;",   "TE;"),
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
