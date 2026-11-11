using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.Tools.Bytecode;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestClass]
	public class KotlinFixupsTests : ClassFileFixture
	{
		[TestMethod]
		public void HideInternalClass ()
		{
			var klass = LoadClassFile ("InternalClass.class");
			var inner_class = klass.InnerClasses.First ();

			Assert.IsTrue (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.IsTrue (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.IsFalse (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.IsFalse (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Public));

			var output = new XmlClassDeclarationBuilder (klass).ToXElement ().ToString ();
			Assert.IsTrue (output.Contains ("visibility=\"private\""));
		}

		[TestMethod]
		public void MakeInternalInterfacePackagePrivate ()
		{
			var klass = LoadClassFile ("InternalInterface.class");
			var inner_class = klass.InnerClasses.First ();

			Assert.IsTrue (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.IsTrue (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			// "package-private" is denoted as no visibility flags
			Assert.IsFalse (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.IsFalse (klass.AccessFlags.HasFlag (ClassAccessFlags.Protected));
			Assert.IsFalse (klass.AccessFlags.HasFlag (ClassAccessFlags.Private));

			Assert.IsFalse (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.IsFalse (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Protected));
			Assert.IsFalse (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Private));
		}

		[TestMethod]
		public void HideInternalConstructor ()
		{
			var klass = LoadClassFile ("InternalConstructor.class");
			var ctor = klass.Methods.First (m => m.Name == "<init>");

			Assert.IsTrue (ctor.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.IsFalse (ctor.AccessFlags.HasFlag (MethodAccessFlags.Public));

			var output = new XmlClassDeclarationBuilder (klass).ToXElement ().ToString ();
			Assert.IsTrue (output.Contains ("visibility=\"kotlin-internal\""));
		}

		[TestMethod]
		public void HideDefaultConstructorMarker ()
		{
			var klass = LoadClassFile ("DefaultConstructor.class");

			// init ()
			var ctor_0p = klass.Methods.Single (m => m.Name == "<init>" && m.GetParameters ().Length == 0);

			// init (string name)
			var ctor_1p = klass.Methods.Single (m => m.Name == "<init>" && m.GetParameters ().Length == 1);

			// init (string p0, int p1, DefaultConstructorMarker p2)
			var ctor_3p = klass.Methods.Single (m => m.Name == "<init>" && m.GetParameters ().Length == 3);

			Assert.IsTrue (ctor_3p.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			// Assert that the normal constructors are still public
			Assert.IsTrue (ctor_0p.AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsTrue (ctor_1p.AccessFlags.HasFlag (MethodAccessFlags.Public));

			// Assert that the synthetic "DefaultConstructorMarker" constructor has been marked private
			Assert.IsFalse (ctor_3p.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[TestMethod]
		public void HidePrivateDefaultConstructorMarker ()
		{
			var klass = LoadClassFile ("PrivateDefaultConstructor.class");

			// init (int isFoo)
			var ctor_1p = klass.Methods.Single (m => m.Name == "<init>" && m.GetParameters ().Length == 1);

			// init (int isFoo, DefaultConstructorMarker p1)
			var ctor_2p = klass.Methods.Single (m => m.Name == "<init>" && m.GetParameters ().Length == 2);

			Assert.IsTrue (ctor_2p.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup ([klass]);

			// Assert that the normal constructor is still public
			Assert.IsFalse (ctor_1p.AccessFlags.HasFlag (MethodAccessFlags.Public));

			// Assert that the synthetic "DefaultConstructorMarker" constructor has been marked private
			Assert.IsFalse (ctor_2p.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[TestMethod]
		public void HideImplementationMethod ()
		{
			var klass = LoadClassFile ("MethodImplementation.class");
			var method = klass.Methods.First (m => m.Name == "toString-impl");

			Assert.IsTrue (method.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.IsFalse (method.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[TestMethod]
		public void RenameExtensionParameter ()
		{
			var klass = LoadClassFile ("RenameExtensionParameterKt.class");
			var method = klass.Methods.First (m => m.Name == "toUtf8String");
			var p = method.GetParameters () [0];

			Assert.AreEqual ("$this$toUtf8String", p.Name);

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("obj", p.Name);
		}

		[TestMethod]
		public void HideInternalMethod ()
		{
			var klass = LoadClassFile ("InternalMethod.class");
			var method = klass.Methods.First (m => m.Name == "take$main");

			Assert.IsTrue (method.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.IsFalse (method.AccessFlags.HasFlag (MethodAccessFlags.Public));

			var output = new XmlClassDeclarationBuilder (klass).ToXElement ().ToString ();
			Assert.IsTrue (output.Contains ("visibility=\"kotlin-internal\""));
		}

		[TestMethod]
		public void HideInternalField ()
		{
			var klass = LoadClassFile ("InternalField.class");
			var field = klass.Fields.First (m => m.Name == "city");

			Assert.IsTrue (field.AccessFlags.HasFlag (FieldAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.IsFalse (field.AccessFlags.HasFlag (FieldAccessFlags.Public));

			var output = new XmlClassDeclarationBuilder (klass).ToXElement ().ToString ();
			Assert.IsTrue (output.Contains ("visibility=\"kotlin-internal\""));
		}

		[TestMethod]
		public void ParameterName ()
		{
			var klass = LoadClassFile ("ParameterName.class");
			var method = klass.Methods.First (m => m.Name == "take");
			var p = method.GetParameters () [0];

			Assert.AreEqual ("p0", p.Name);

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("count", p.Name);
		}

		[TestMethod]
		public void HideInternalProperty ()
		{
			var klass = LoadClassFile ("InternalProperty.class");
			var getter = klass.Methods.First (m => m.Name == "getCity$main");
			var setter = klass.Methods.First (m => m.Name == "setCity$main");

			Assert.IsTrue (getter.AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsTrue (setter.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.IsFalse (getter.AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsFalse (setter.AccessFlags.HasFlag (MethodAccessFlags.Public));

			var output = new XmlClassDeclarationBuilder (klass).ToXElement ().ToString ();
			Assert.IsTrue (output.Contains ("visibility=\"kotlin-internal\""));
		}

		[TestMethod]
		public void RenameSetterParameter ()
		{
			var klass = LoadClassFile ("SetterParameterName.class");
			var setter = klass.Methods.First (m => m.Name == "setCity");
			var p = setter.GetParameters () [0];

			Assert.AreEqual ("p0", p.Name);

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("value", p.Name);
		}

		[TestMethod]
		public void UnsignedMethods ()
		{
			var klass = LoadClassFile ("UnsignedTypes.class");

			var uint_method = klass.Methods.Single (m => m.Name.Contains ("foo_uint-"));
			var ushort_method = klass.Methods.Single (m => m.Name.Contains ("foo_ushort-"));
			var ulong_method = klass.Methods.Single (m => m.Name.Contains ("foo_ulong-"));
			var ubyte_method = klass.Methods.Single (m => m.Name.Contains ("foo_ubyte-"));
			var uintarray_method = klass.Methods.Single (m => m.Name.Contains ("foo_uintarray-"));
			var ushortarray_method = klass.Methods.Single (m => m.Name.Contains ("foo_ushortarray-"));
			var ulongarray_method = klass.Methods.Single (m => m.Name.Contains ("foo_ulongarray-"));
			var ubytearray_method = klass.Methods.Single (m => m.Name.Contains ("foo_ubytearray-"));
			var uintarrayarray_method = klass.Methods.Single (m => m.Name.Contains ("foo_uintarrayarray"));

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("uint", uint_method.GetParameters () [0].KotlinType);
			Assert.AreEqual ("uint", uint_method.KotlinReturnType);

			Assert.AreEqual ("ushort", ushort_method.GetParameters () [0].KotlinType);
			Assert.AreEqual ("ushort", ushort_method.KotlinReturnType);

			Assert.AreEqual ("ulong", ulong_method.GetParameters () [0].KotlinType);
			Assert.AreEqual ("ulong", ulong_method.KotlinReturnType);

			Assert.AreEqual ("ubyte", ubyte_method.GetParameters () [0].KotlinType);
			Assert.AreEqual ("ubyte", ubyte_method.KotlinReturnType);

			Assert.AreEqual ("uint[]", uintarray_method.GetParameters () [0].KotlinType);
			Assert.AreEqual ("uint[]", uintarray_method.KotlinReturnType);

			Assert.AreEqual ("ushort[]", ushortarray_method.GetParameters () [0].KotlinType);
			Assert.AreEqual ("ushort[]", ushortarray_method.KotlinReturnType);

			Assert.AreEqual ("ulong[]", ulongarray_method.GetParameters () [0].KotlinType);
			Assert.AreEqual ("ulong[]", ulongarray_method.KotlinReturnType);

			Assert.AreEqual ("ubyte[]", ubytearray_method.GetParameters () [0].KotlinType);
			Assert.AreEqual ("ubyte[]", ubytearray_method.KotlinReturnType);

			// Kotlin's Array<UIntArray> does not trigger this code because it is not
			// encoded as Java's "[[I", instead it is exposed as "UIntArray[]", so
			// we treat it as a normal class array.
			Assert.IsNull (uintarrayarray_method.GetParameters () [0].KotlinType);
			Assert.IsNull (uintarrayarray_method.KotlinReturnType);
		}

		[TestMethod]
		public void UnsignedFields ()
		{
			var klass = LoadClassFile ("UnsignedTypesKt.class");

			var uint_field = klass.Fields.Single (m => m.Name == "UINT_CONST");
			var ushort_field = klass.Fields.Single (m => m.Name == "USHORT_CONST");
			var ulong_field = klass.Fields.Single (m => m.Name == "ULONG_CONST");
			var ubyte_field = klass.Fields.Single (m => m.Name == "UBYTE_CONST");

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("uint", uint_field.KotlinType);
			Assert.AreEqual ("ushort", ushort_field.KotlinType);
			Assert.AreEqual ("ulong", ulong_field.KotlinType);
			Assert.AreEqual ("ubyte", ubyte_field.KotlinType);
		}

		[TestMethod]
		public void UnsignedFieldsXml ()
		{
			// Ensure Kotlin unsigned types end up in the xml
			var klass = LoadClassFile ("UnsignedTypesKt.class");

			KotlinFixups.Fixup (new [] { klass });

			var xml = new XmlClassDeclarationBuilder (klass).ToXElement ();

			Assert.AreEqual ("uint", xml.Elements ("field").Single (f => f.Attribute ("name").Value == "UINT_CONST").Attribute ("type").Value);
			Assert.AreEqual ("ushort", xml.Elements ("field").Single (f => f.Attribute ("name").Value == "USHORT_CONST").Attribute ("type").Value);
			Assert.AreEqual ("ulong", xml.Elements ("field").Single (f => f.Attribute ("name").Value == "ULONG_CONST").Attribute ("type").Value);
			Assert.AreEqual ("ubyte", xml.Elements ("field").Single (f => f.Attribute ("name").Value == "UBYTE_CONST").Attribute ("type").Value);
		}

		[TestMethod]
		public void UnsignedMethodsXml ()
		{
			// Ensure Kotlin unsigned types end up in the xml
			var klass = LoadClassFile ("UnsignedTypes.class");

			KotlinFixups.Fixup (new [] { klass });

			var xml = new XmlClassDeclarationBuilder (klass).ToXElement ();

			Assert.AreEqual ("uint", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_uint-WZ4Q5Ns").Attribute ("return").Value);
			Assert.AreEqual ("uint", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_uint-WZ4Q5Ns").Element ("parameter").Attribute ("type").Value);

			Assert.AreEqual ("ushort", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ushort-xj2QHRw").Attribute ("return").Value);
			Assert.AreEqual ("ushort", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ushort-xj2QHRw").Element ("parameter").Attribute ("type").Value);

			Assert.AreEqual ("ulong", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ulong-VKZWuLQ").Attribute ("return").Value);
			Assert.AreEqual ("ulong", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ulong-VKZWuLQ").Element ("parameter").Attribute ("type").Value);

			Assert.AreEqual ("ubyte", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ubyte-7apg3OU").Attribute ("return").Value);
			Assert.AreEqual ("ubyte", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ubyte-7apg3OU").Element ("parameter").Attribute ("type").Value);

			Assert.AreEqual ("uint[]", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_uintarray--ajY-9A").Attribute ("return").Value);
			Assert.AreEqual ("uint[]", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_uintarray--ajY-9A").Element ("parameter").Attribute ("type").Value);

			Assert.AreEqual ("ushort[]", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ushortarray-rL5Bavg").Attribute ("return").Value);
			Assert.AreEqual ("ushort[]", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ushortarray-rL5Bavg").Element ("parameter").Attribute ("type").Value);

			Assert.AreEqual ("ulong[]", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ulongarray-QwZRm1k").Attribute ("return").Value);
			Assert.AreEqual ("ulong[]", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ulongarray-QwZRm1k").Element ("parameter").Attribute ("type").Value);

			Assert.AreEqual ("ubyte[]", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ubytearray-GBYM_sE").Attribute ("return").Value);
			Assert.AreEqual ("ubyte[]", xml.Elements ("method").Single (f => f.Attribute ("name").Value == "foo_ubytearray-GBYM_sE").Element ("parameter").Attribute ("type").Value);
		}

		[TestMethod]
		public void HandleKotlinNameShadowing ()
		{
			var klass = LoadClassFile ("NameShadowing.class");

			KotlinFixups.Fixup (new [] { klass });

			Assert.IsTrue (klass.Methods.Single (m => m.Name == "count").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsTrue (klass.Methods.Single (m => m.Name == "hitCount").AccessFlags.HasFlag (MethodAccessFlags.Public));

			// Private property and explicit getter/setter
			// There is no generated getter/setter, and explicit ones should be public
			Assert.IsTrue (klass.Methods.Single (m => m.Name == "getType").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsTrue (klass.Methods.Single (m => m.Name == "setType").AccessFlags.HasFlag (MethodAccessFlags.Public));

			// Private immutable property and explicit getter/setter
			// There is no generated getter/setter, and explicit ones should be public
			Assert.IsTrue (klass.Methods.Single (m => m.Name == "getType2").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsTrue (klass.Methods.Single (m => m.Name == "setType2").AccessFlags.HasFlag (MethodAccessFlags.Public));

			// Internal property and explicit getter/setter
			// Generated getter/setter should not be public, and explicit ones should be public
			Assert.IsFalse (klass.Methods.Single (m => m.Name == "getItype$main").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsFalse (klass.Methods.Single (m => m.Name == "setItype$main").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsTrue (klass.Methods.Single (m => m.Name == "getItype").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsTrue (klass.Methods.Single (m => m.Name == "setItype").AccessFlags.HasFlag (MethodAccessFlags.Public));

			// Internal immutable property and explicit getter/setter
			// Generated getter should not be public, and explicit ones should be public
			Assert.IsFalse (klass.Methods.Single (m => m.Name == "getItype2$main").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsTrue (klass.Methods.Single (m => m.Name == "getItype2").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsTrue (klass.Methods.Single (m => m.Name == "setItype2").AccessFlags.HasFlag (MethodAccessFlags.Public));

			// Internal property with unsigned type
			// Generated getter/setter should not be public
			Assert.IsFalse (klass.Methods.Single (m => m.Name == "getUnsignedInternalProperty-pVg5ArA$main").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.IsFalse (klass.Methods.Single (m => m.Name == "setUnsignedInternalProperty-WZ4Q5Ns$main").AccessFlags.HasFlag (MethodAccessFlags.Public));

			// Public property with unsigned type
			// We want to check that KotlinType/KotlinReturnType are filled it as it proves our FindJavaProperty[Getter|Setter] functions are matching
			// (We aren't changing the visibility of the getter/setter, so we can't just check the access flags)
			Assert.AreEqual ("uint", klass.Methods.Single (m => m.Name == "getUnsignedPublicProperty-pVg5ArA").KotlinReturnType);
			Assert.AreEqual ("uint", klass.Methods.Single (m => m.Name == "setUnsignedPublicProperty-WZ4Q5Ns").GetParameters () [0].KotlinType);
		}

		[TestMethod]
		public void MatchParametersWithReceiver ()
		{
			var klass = LoadClassFile ("DeepRecursiveKt.class");
			var meta = GetFileMetadataForClass (klass);

			var java_method = klass.Methods.Single (m => m.Name == "invoke");
			var kotlin_function = meta.Functions.Single (m => m.Name == "invoke");

			(var start, var end) = KotlinFixups.CreateParameterMap (java_method, kotlin_function, null);

			// Start is 1 instead of 0 to skip the receiver
			Assert.AreEqual (1, start);
			Assert.AreEqual (2, end);
		}

		[TestMethod]
		public void MatchParametersWithContinuation ()
		{
			var klass = LoadClassFile ("DeepRecursiveScope.class");
			var meta = GetClassMetadataForClass (klass);

			var java_method = klass.Methods.Single (m => m.Name == "callRecursive" && m.GetParameters ().Count () == 2);
			var kotlin_function = meta.Functions.Single (m => m.Name == "callRecursive" && m.JvmSignature.Count (c => c == ';') == 3);

			(var start, var end) = KotlinFixups.CreateParameterMap (java_method, kotlin_function, null);

			// End is 1 instead of 2 to skip the trailing continuation
			Assert.AreEqual (0, start);
			Assert.AreEqual (1, end);
		}

		[TestMethod]
		public void MatchParametersWithStaticThis ()
		{
			var klass = LoadClassFile ("UByteArray.class");
			var meta = GetClassMetadataForClass (klass);

			var java_method = klass.Methods.Single (m => m.Name == "contains-7apg3OU" && m.GetParameters ().Count () == 2);
			var kotlin_function = meta.Functions.Single (m => m.Name == "contains");

			(var start, var end) = KotlinFixups.CreateParameterMap (java_method, kotlin_function, null);

			// Start is 1 instead of 0 to skip the static 'this'
			Assert.AreEqual (1, start);
			Assert.AreEqual (2, end);
		}

		[TestMethod]
		public void MatchMetadataToMultipleMethodsWithSameMangledName ()
		{
			var klass = LoadClassFile ("UByteArray.class");
			var meta = GetClassMetadataForClass (klass);

			// There is only one Kotlin metadata for Java method "contains-7apg3OU"
			var kotlin_function = meta.Functions.Single (m => m.Name == "contains");
			
			// However there are 2 Java methods named "contains-7apg3OU"
			// - public boolean contains-7apg3OU (byte element)
			// - public static boolean contains-7apg3OU (byte[] arg0, byte element)
			var java_methods = klass.Methods.Where (m => m.Name == "contains-7apg3OU");
			Assert.AreEqual (2, java_methods.Count ());

			KotlinFixups.Fixup (new [] { klass });

			// Ensure the fixup "fixed" the parameter "element" type to "ubyte"
			Assert.AreEqual ("ubyte", java_methods.ElementAt (0).GetParameters ().Single (p => p.Name == "element").KotlinType);
			Assert.AreEqual ("ubyte", java_methods.ElementAt (1).GetParameters ().Single (p => p.Name == "element").KotlinType);
		}

		[TestMethod]
		public void GetMethodNameWithoutUnsignedSuffix ()
		{
			// Just a few quick tests to ensure the various cases are covered
			Assert.AreEqual ("setFoo", KotlinUtilities.GetMethodNameWithoutUnsignedSuffix ("setFoo"));
			Assert.AreEqual ("setFoo", KotlinUtilities.GetMethodNameWithoutUnsignedSuffix ("setFoo-7apg3OU"));
			Assert.AreEqual ("setFoo$main", KotlinUtilities.GetMethodNameWithoutUnsignedSuffix ("setFoo-7apg3OU$main"));
			Assert.AreEqual ("setFoo$main", KotlinUtilities.GetMethodNameWithoutUnsignedSuffix ("setFoo$main"));
		}
	}
}
