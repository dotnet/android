using System;
using System.Collections.Generic;
using System.Linq;

using Xamarin.Android.Tools.Bytecode;

using NUnit.Framework;

using Assembly = System.Reflection.Assembly;

namespace Xamarin.Android.Tools.BytecodeTests {

	[TestFixture]
	public class JavaTypeTests : ClassFileFixture {

		[Test]
		public void ClassFile_WithJavaType_class ()
		{
			var c   = LoadClassFile ("JavaType.class");
			new ExpectedTypeDeclaration {
				MajorVersion        = 0x32,
				MinorVersion        = 0,
				ConstantPoolCount   = 195,
				AccessFlags         = ClassAccessFlags.Public | ClassAccessFlags.Super,
				FullName            = "com/xamarin/JavaType",
				Superclass          = new TypeInfo ("java/lang/Object", "Ljava/lang/Object;"),
				TypeParameters = {
					new TypeParameterInfo {
						Identifier  = "E",
						ClassBound  = "Ljava/lang/Object;",
					},
				},
				Interfaces = {
					new TypeInfo ("java/lang/Cloneable",    "Ljava/lang/Cloneable;"),
					new TypeInfo ("java/lang/Comparable",   "Ljava/lang/Comparable<Lcom/xamarin/JavaType<TE;>;>;"),
					new TypeInfo ("com/xamarin/IJavaInterface", "Lcom/xamarin/IJavaInterface<Ljava/lang/StringBuilder;Ljava/util/ArrayList<Ljava/lang/StringBuilder;>;Ljava/util/List<Ljava/lang/String;>;>;"),
				},
				InnerClasses = {
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$ASC",
						OuterClassName  = "com/xamarin/JavaType",
						InnerName       = "ASC",
						AccessFlags     = ClassAccessFlags.Static,
					},
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$RNC",
						OuterClassName  = "com/xamarin/JavaType",
						InnerName       = "RNC",
						AccessFlags     = ClassAccessFlags.Protected | ClassAccessFlags.Abstract,
					},
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$PSC",
						OuterClassName  = "com/xamarin/JavaType",
						InnerName       = "PSC",
						AccessFlags     = ClassAccessFlags.Public | ClassAccessFlags.Static | ClassAccessFlags.Abstract,
					},
					new ExpectedInnerClassInfo {
						InnerClassName  = "com/xamarin/JavaType$1",
						OuterClassName  = null,
						InnerName       = null,
						AccessFlags     = 0,
					},
				},
				Fields = {
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_OBJECT",
						Descriptor          = "Ljava/lang/Object;",
						Deprecated          = true,
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_INT32",
						Descriptor          = "I",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Integer(42)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_INT32_MIN",
						Descriptor          = "I",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Integer(-2147483648)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_INT32_MAX",
						Descriptor          = "I",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Integer(2147483647)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_CHAR_MIN",
						Descriptor          = "C",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Integer(0)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_CHAR_MAX",
						Descriptor          = "C",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Integer(65535)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_INT64_MIN",
						Descriptor          = "J",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Long(-9223372036854775808)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_INT64_MAX",
						Descriptor          = "J",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Long(9223372036854775807)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_SINGLE_MIN",
						Descriptor          = "F",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Float(1.401298E-45)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_SINGLE_MAX",
						Descriptor          = "F",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Float(3.402823E+38)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_DOUBLE_MIN",
						Descriptor          = "D",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Double(4.94065645841247E-324)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_DOUBLE_MAX",
						Descriptor          = "D",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Double(1.79769313486232E+308)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_STRING",
						Descriptor          = "Ljava/lang/String;",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "String(stringIndex=185 Utf8=\"Hello, \\\"embedded\0Nulls\" and \ud83d\udca9!\")",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_BOOL_FALSE",
						Descriptor          = "Z",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Integer(0)",
					},
					new ExpectedFieldDeclaration {
						Name                = "STATIC_FINAL_BOOL_TRUE",
						Descriptor          = "Z",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Integer(1)",
					},
					new ExpectedFieldDeclaration {
						Name                = "POSITIVE_INFINITY",
						Descriptor          = "D",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Double(Infinity)",
					},
					new ExpectedFieldDeclaration {
						Name                = "NEGATIVE_INFINITY",
						Descriptor          = "D",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Double(-Infinity)",
					},
					new ExpectedFieldDeclaration {
						Name                = "NaN",
						Descriptor          = "D",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Static | FieldAccessFlags.Final,
						ConstantValue       = "Double(NaN)",
					},
					new ExpectedFieldDeclaration {
						Name                = "INSTANCE_FINAL_OBJECT",
						Descriptor          = "Ljava/lang/Object;",
						Deprecated          = true,
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Final,
					},
					new ExpectedFieldDeclaration {
						Name                = "INSTANCE_FINAL_E",
						Descriptor          = "Ljava/lang/Object;",
						GenericDescriptor   = "TE;",
						AccessFlags         = FieldAccessFlags.Public | FieldAccessFlags.Final,
					},
					new ExpectedFieldDeclaration {
						Name                = "packageInstanceEArray",
						Descriptor          = "[Ljava/lang/Object;",
						GenericDescriptor   = "[TE;",
						AccessFlags         = 0,
					},
					new ExpectedFieldDeclaration {
						Name                = "protectedInstanceEList",
						Descriptor          = "Ljava/util/List;",
						GenericDescriptor   = "Ljava/util/List<TE;>;",
						AccessFlags         = FieldAccessFlags.Protected,
					},
					new ExpectedFieldDeclaration {
						Name                = "privateInstanceArrayOfListOfIntArrayArray",
						Descriptor          = "[Ljava/util/List;",
						GenericDescriptor   = "[Ljava/util/List<[[I>;",
						AccessFlags         = FieldAccessFlags.Private,
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
							new ParameterInfo ("value", "Ljava/lang/String;",     "Ljava/lang/String;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "compareTo",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "I",
						Parameters = {
							new ParameterInfo ("value", "Lcom/xamarin/JavaType;", "Lcom/xamarin/JavaType<TE;>;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "func",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "Ljava/util/List;",
						ReturnGenericDescriptor = "Ljava/util/List<Ljava/lang/String;>;",
						Parameters = {
							new ParameterInfo ("value", "Ljava/lang/StringBuilder;",    "Ljava/lang/StringBuilder;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "run",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
					},
					new ExpectedMethodDeclaration {
						Name                    = "action",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						Deprecated              = true,
						Parameters = {
							new ParameterInfo { Name = "value", Type = new TypeInfo ("Ljava/lang/Object;", "Ljava/lang/Object;") },
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "func",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "Ljava/lang/Integer;",
						Parameters = {
							new ParameterInfo ("values",    "[Ljava/lang/String;", "[Ljava/lang/String;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "staticActionWithGenerics",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Static,
						ReturnDescriptor        = "V",
						TypeParameters = {
							new TypeParameterInfo ("T",               "Ljava/lang/Object;"),
							new TypeParameterInfo ("TExtendsNumber",  "Ljava/lang/Number;",  new [] { "Ljava/lang/Comparable<TT;>;" }),
							new TypeParameterInfo ("TThrowable",      "Ljava/lang/Throwable;"),
						},
						Parameters = {
							new ParameterInfo ("value1",        "Ljava/lang/Object;",  "TT;"),
							new ParameterInfo ("value2",        "Ljava/lang/Number;",  "TTExtendsNumber;"),
							new ParameterInfo ("unboundedList", "Ljava/util/List;",    "Ljava/util/List<*>;"),
							new ParameterInfo ("extendsList",   "Ljava/util/List;",    "Ljava/util/List<+Ljava/lang/Number;>;"),
							new ParameterInfo ("superList",     "Ljava/util/List;",    "Ljava/util/List<-Ljava/lang/Throwable;>;"),
						},
						Throws = {
							new TypeInfo ("java/lang/IllegalArgumentException", "Ljava/lang/IllegalArgumentException;"),
							new TypeInfo ("java/lang/NumberFormatException",    "Ljava/lang/NumberFormatException;"),
							new TypeInfo ("java/lang/Throwable",                "TTThrowable;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "instanceActionWithGenerics",
						AccessFlags             = MethodAccessFlags.Public,
						ReturnDescriptor        = "V",
						TypeParameters = {
							new TypeParameterInfo ("T",               "Ljava/lang/Object;"),
						},
						Parameters = {
							new ParameterInfo ("value1",        "Ljava/lang/Object;",  "TT;"),
							new ParameterInfo ("value2",        "Ljava/lang/Object;",  "TE;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "sum",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Static | MethodAccessFlags.Varargs,
						ReturnDescriptor        = "I",
						Parameters = {
							new ParameterInfo ("first",     "I",    "I"),
							new ParameterInfo ("remaining", "[I",   "[I"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "finalize",
						AccessFlags             = MethodAccessFlags.Protected,
						ReturnDescriptor        = "V",
					},
					new ExpectedMethodDeclaration {
						Name                    = "finalize",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Static,
						ReturnDescriptor        = "I",
						Parameters = {
							new ParameterInfo ("value", "I",    "I"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "compareTo",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Bridge | MethodAccessFlags.Synthetic,
						ReturnDescriptor        = "I",
						Parameters = {
							new ParameterInfo ("value", "Ljava/lang/Object;", "Ljava/lang/Object;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "func",
						AccessFlags             = MethodAccessFlags.Public | MethodAccessFlags.Bridge | MethodAccessFlags.Synthetic,
						ReturnDescriptor        = "Ljava/lang/Object;",
						Parameters = {
							new ParameterInfo ("value", "Ljava/lang/CharSequence;", "Ljava/lang/CharSequence;"),
						},
					},
					new ExpectedMethodDeclaration {
						Name                    = "<clinit>",
						AccessFlags             = MethodAccessFlags.Static,
						ReturnDescriptor        = "V",
					},
				},
			}.Assert (c);
		}

		[Test]
		public void XmlDeclaration_WithJavaType_class ()
		{
			AssertXmlDeclaration ("JavaType.class", "JavaType.xml");
		}
	}
}

