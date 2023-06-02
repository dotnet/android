using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaType_1MyStringListTests : ClassFileFixture {

		const string JavaType = "JavaType$1MyStringList";

		static readonly bool Jdk8   = ConfiguredJdkInfo.Version == null
				? false
				: ConfiguredJdkInfo.Version < new Version (1, 9);

		[Test]
		public void ClassFileDescription ()
		{
			var vallist = Jdk8
				? new ParameterInfo ("val$value1",        "Ljava/util/List;",     "Ljava/util/List;")
				: new ParameterInfo ("val$unboundedList", "Ljava/util/List;",     "Ljava/util/List;");
			var valobj  = Jdk8
				? new ParameterInfo ("val$unboundedList", "Ljava/lang/Object;",   "Ljava/lang/Object;")
				: new ParameterInfo ("val$value1",        "Ljava/lang/Object;",   "Ljava/lang/Object;");
			var c       = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x37,
				MinorVersion        = 0,
				ConstantPoolCount   = 74,
				AccessFlags         = ClassAccessFlags.Super,
				FullName            = "com/xamarin/JavaType$1MyStringList",
				Superclass          = new TypeInfo ("java/util/ArrayList", "Ljava/util/ArrayList<Ljava/lang/String;>;"),
				InnerClasses = {
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$1MyStringList",
						OuterClassName  = null,
						InnerName       = "MyStringList",
						AccessFlags     = 0,
					},
				},
				Fields = {
					new ExpectedFieldDeclaration {
						Name                = "val$unboundedList",
						Descriptor          = "Ljava/util/List;",
						AccessFlags         = FieldAccessFlags.Final | FieldAccessFlags.Synthetic,
					},
					new ExpectedFieldDeclaration {
						Name                = "val$value1",
						Descriptor          = "Ljava/lang/Object;",
						AccessFlags         = FieldAccessFlags.Final | FieldAccessFlags.Synthetic,
					},
				},
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							// "actual" parameters:
							new ParameterInfo ("a",                 "Ljava/lang/String;",   "Ljava/lang/String;"),
							new ParameterInfo ("b",                 "I",                    "I"),
							// declaring method parameters:
							vallist,
							valobj,
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							// "actual" parameters:
							new ParameterInfo ("value1",            "Ljava/lang/Object;",   "TT;"),
							new ParameterInfo ("a",                 "Ljava/lang/String;",   "Ljava/lang/String;"),
							new ParameterInfo ("b",                 "I",                    "I"),
							// declaring method parameters:
							vallist,
							valobj,
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							// "actual" parameters:
							new ParameterInfo ("a",                 "Ljava/lang/String;",   "Ljava/lang/String;"),
							new ParameterInfo ("value2",            "Ljava/lang/Number;",   "TTExtendsNumber;"),
							new ParameterInfo ("b",                 "I",                    "I"),
							// declaring method parameters:
							vallist,
							valobj,
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "<init>",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Parameters = {
							// "actual" parameters:
							new ParameterInfo ("a",                 "Ljava/lang/String;",   "Ljava/lang/String;"),
							new ParameterInfo ("b",                 "I",                    "I"),
							new ParameterInfo ("unboundedList",     "Ljava/util/List;",     "Ljava/util/List<*>;"),
							// declaring method parameters:
							vallist,
							valobj,
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "get",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "Ljava/lang/String;",
						Parameters = {
							new ParameterInfo ("index", "I",     "I"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "get",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Bridge | MethodAccessFlags.Synthetic,
						ReturnDescriptor        = "Ljava/lang/Object;",
						Parameters = {
							new ParameterInfo ("index", "I",     "I"),
						},
					},
				}
			}.Assert (c);
		}

		[Test]
		public void XmlDescription ()
		{
			var resourceName = JavaType + (Jdk8 ? "-1.8" : "") + ".xml";
			AssertXmlDeclaration (JavaType + ".class", resourceName);
		}
	}
}
