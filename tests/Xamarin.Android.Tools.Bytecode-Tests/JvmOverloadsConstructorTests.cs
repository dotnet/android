using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

using Assembly = System.Reflection.Assembly;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JvmOverloadsConstructorTests : ClassFileFixture {

		const   string  ClassFile   = "JvmOverloadsConstructor.class";
		const   string  XmlFile     = "JvmOverloadsConstructor.xml";

		[Test]
		public void ClassFile_WithJavaType_class ()
		{
			var c   = LoadClassFile (ClassFile);
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 59,
				AccessFlags         = ClassAccessFlags.Public | ClassAccessFlags.Final | ClassAccessFlags.Super,
				FullName            = "JvmOverloadsConstructor",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							new ParameterInfo ("something",     "LJvmOverloadsConstructor;",    "LJvmOverloadsConstructor;"),
							new ParameterInfo ("id",            "I",                            "I"),
							new ParameterInfo ("imageId",       "I",                            "I"),
							new ParameterInfo ("title",         "Ljava/lang/String;",           "Ljava/lang/String;"),
							new ParameterInfo ("useDivider",    "Z",                            "Z"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Synthetic,
						ReturnDescriptor        = "V",
						Parameters = {
							new ParameterInfo ("p0",            "LJvmOverloadsConstructor;",    "LJvmOverloadsConstructor;"),
							new ParameterInfo ("p1",            "I",                            "I"),
							new ParameterInfo ("p2",            "I",                            "I"),
							new ParameterInfo ("p3",            "Ljava/lang/String;",           "Ljava/lang/String;"),
							new ParameterInfo ("p4",            "Z",                            "Z"),
							new ParameterInfo ("p5",            "I",                            "I"),
							new ParameterInfo ("p6",            "Lkotlin/jvm/internal/DefaultConstructorMarker;",   "Lkotlin/jvm/internal/DefaultConstructorMarker;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							new ParameterInfo ("something",     "LJvmOverloadsConstructor;",    "LJvmOverloadsConstructor;"),
							new ParameterInfo ("id",            "I",                            "I"),
							new ParameterInfo ("imageId",       "I",                            "I"),
							new ParameterInfo ("title",         "Ljava/lang/String;",           "Ljava/lang/String;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							new ParameterInfo ("something",     "LJvmOverloadsConstructor;",    "LJvmOverloadsConstructor;"),
							new ParameterInfo ("id",            "I",                            "I"),
							new ParameterInfo ("title",         "Ljava/lang/String;",           "Ljava/lang/String;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							new ParameterInfo ("something",     "LJvmOverloadsConstructor;",    "LJvmOverloadsConstructor;"),
							new ParameterInfo ("title",         "Ljava/lang/String;",           "Ljava/lang/String;"),
						},
					},
				},
			}.Assert (c);
		}

		[Test]
		public void XmlDeclaration_WithJavaType_class ()
		{
			AssertXmlDeclaration (ClassFile, XmlFile);
		}
	}
}

