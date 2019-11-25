using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

using Assembly = System.Reflection.Assembly;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaEnumTests : ClassFileFixture {

		const string JavaType = "JavaEnum";

		[Test]
		public void ClassFileDescription ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 53,
				AccessFlags         = ClassAccessFlags.Final | ClassAccessFlags.Super | ClassAccessFlags.Enum,
				FullName            = "com/xamarin/JavaEnum",
				Superclass          = new TypeInfo ("java/lang/Enum",   "Ljava/lang/Enum<Lcom/xamarin/JavaEnum;>;"),
				Fields = {
					new ExpectedFieldDeclaration {
						Name                = "FIRST",
						Descriptor          = "Lcom/xamarin/JavaEnum;",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final | FieldAccessFlags.Enum,
					},
					new ExpectedFieldDeclaration {
						Name                = "SECOND",
						Descriptor          = "Lcom/xamarin/JavaEnum;",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final | FieldAccessFlags.Enum,
					},
					new ExpectedFieldDeclaration {
						Name                = "$VALUES",
						Descriptor          = "[Lcom/xamarin/JavaEnum;",
						AccessFlags         = FieldAccessFlags.Private | FieldAccessFlags.Static | FieldAccessFlags.Final | FieldAccessFlags.Synthetic,
					},
				},
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "values",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Static,
						ReturnDescriptor        = "[Lcom/xamarin/JavaEnum;",
					},
					new ExpectedMethodDeclaration {
						Name                    = "valueOf",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Static,
						ReturnDescriptor        = "Lcom/xamarin/JavaEnum;",
						Parameters = {
							new ParameterInfo ("name",  "Ljava/lang/String;",  "Ljava/lang/String;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Private,
						ReturnDescriptor        = "V",
						Parameters = {
							new ParameterInfo ("$enum$name",    "Ljava/lang/String;",  "Ljava/lang/String;"),
							new ParameterInfo ("$enum$ordinal", "I",                   "I"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "switchValue",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "I",
					},
					new ExpectedMethodDeclaration {
						Name                    = "<clinit>",
						AccessFlags             = MethodAccessFlags.Static,
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

