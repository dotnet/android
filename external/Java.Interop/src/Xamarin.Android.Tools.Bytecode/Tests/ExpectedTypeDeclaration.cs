using System;
using System.Collections.Generic;

using Xamarin.Android.Tools.Bytecode;

using NAssert   = NUnit.Framework.Assert;

namespace Xamarin.Android.Tools.BytecodeTests {

	class ExpectedTypeDeclaration {

		public  ushort                              MajorVersion;
		public  ushort                              MinorVersion;
		public  int                                 ConstantPoolCount;
		public  ClassAccessFlags                    AccessFlags;
		public  string                              FullName;
		public  TypeInfo                            Superclass;
		public  bool                                Deprecated;
		public  List<TypeInfo>                      Interfaces      = new List<TypeInfo> ();
		public  List<ExpectedInnerClassInfo>        InnerClasses    = new List<ExpectedInnerClassInfo> ();
		public  List<TypeParameterInfo>             TypeParameters  = new List<TypeParameterInfo> ();
		public  List<ExpectedFieldDeclaration>      Fields          = new List<ExpectedFieldDeclaration> ();
		public  List<ExpectedMethodDeclaration>     Methods         = new List<ExpectedMethodDeclaration> ();

		public void Assert (ClassFile classDeclaration)
		{
			NAssert.AreEqual (MajorVersion,             classDeclaration.MajorVersion,          FullName + " Major Version");
			NAssert.AreEqual (MinorVersion,             classDeclaration.MinorVersion,          FullName + " Minor Version");
			NAssert.AreEqual (ConstantPoolCount,        classDeclaration.ConstantPool.Count,    FullName + " ConstantPool Count");
			NAssert.AreEqual (AccessFlags,              classDeclaration.AccessFlags,           FullName + " AccessFlags");
			NAssert.AreEqual (FullName,                 classDeclaration.ThisClass.Name.Value,  FullName + " Name");
			NAssert.AreEqual (Superclass.BinaryName,    classDeclaration.SuperClass.Name.Value, FullName + " SuperClass Name");

			NAssert.AreEqual (Deprecated,   classDeclaration.Attributes.Get<DeprecatedAttribute> () != null,    FullName + " Deprecated");

			NAssert.AreEqual (Interfaces.Count,         classDeclaration.Interfaces.Count,      $"{FullName} Interfaces Count");
			for (int i = 0; i < Interfaces.Count; ++i) {
				NAssert.AreEqual (Interfaces [i].BinaryName,    classDeclaration.Interfaces [i].Name.Value,     $"{FullName} Interface {i}");
			}

			var innerClasses    = classDeclaration.InnerClasses;
			NAssert.AreEqual (InnerClasses.Count, innerClasses.Count,   FullName + " Inner Classes");
			for (int i = 0; i < InnerClasses.Count; ++i)
				InnerClasses [i].Assert (innerClasses [i]);

			var interfaces  = classDeclaration.GetInterfaces ();
			for (int i = 0; i < Interfaces.Count; ++i) {
				NAssert.AreEqual (Interfaces [i].BinaryName,    interfaces [i].BinaryName,      FullName + " Interfaces BinaryName");
				NAssert.AreEqual (Interfaces [i].TypeSignature, interfaces [i].TypeSignature,   FullName + " Interfaces TypeSignature");
			}

			var signature   = classDeclaration.GetSignature ();
			if (signature != null) {
				NAssert.AreEqual (Superclass.TypeSignature, signature.SuperclassSignature,  FullName + " SuperclassSignature");
				NAssert.AreEqual (TypeParameters.Count,     signature.TypeParameters.Count, FullName + " TypeParameters count");
				for (int i = 0; i < TypeParameters.Count; ++i) {
					NAssert.AreEqual (TypeParameters [i].ToString (),   signature.TypeParameters [i].ToString (),   FullName + " Type Parameter");
				}
			}

			NAssert.AreEqual (Fields.Count, classDeclaration.Fields.Count,  FullName + " Fields Count");
			for (int i = 0; i < Fields.Count; ++i) {
				Fields [i].Assert (classDeclaration.Fields [i]);
			}

			NAssert.AreEqual (Methods.Count, classDeclaration.Methods.Count,    FullName + " Methods Count");
			for (int i = 0; i < Methods.Count; ++i) {
				Methods [i].Assert (classDeclaration.Methods [i]);
			}
		}
	}
}

