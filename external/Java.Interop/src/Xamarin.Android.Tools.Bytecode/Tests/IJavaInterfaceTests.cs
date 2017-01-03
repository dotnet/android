using System;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class IJavaInterfaceTests : ClassFileFixture {

		const string JavaType = "IJavaInterface";

		[Test]
		public void ClassFile_WithIJavaInterface_class ()
		{
			var c   = LoadClassFile (JavaType + ".class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 23,
				AccessFlags         = ClassAccessFlags.Interface | ClassAccessFlags.Abstract,
				FullName            = "com/xamarin/IJavaInterface",
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
				Interfaces = {
					new TypeInfo ("java/lang/Runnable",     "Ljava/lang/Runnable;"),
				},
				Fields = {
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_INT",
						Descriptor          = "I",
						ConstantValue       = "Integer(1)",
						Deprecated          = true,
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
					},
				},
				Methods = {
					new ExpectedMethodDeclaration {
						Name                    = "func",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Abstract,
						ReturnDescriptor        = "Ljava/lang/Object;",
						ReturnGenericDescriptor = "TTReturn;",
						Parameters = {
							new ParameterInfo ("value",     "Ljava/lang/CharSequence;", "TTString;"),
						},
					},
				}
			}.Assert (c);
		}

		[Test]
		public void XmlDeclaration_WithIJavaInterface_class ()
		{
			AssertXmlDeclaration (JavaType + ".class", JavaType + ".xml");
		}
	}
}

