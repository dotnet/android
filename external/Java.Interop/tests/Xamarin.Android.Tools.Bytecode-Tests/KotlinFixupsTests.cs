using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using Xamarin.Android.Tools.Bytecode;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestFixture]
	public class KotlinFixupsTests : ClassFileFixture
	{
		[Test]
		public void HideInternalClass ()
		{
			var klass = LoadClassFile ("InternalClass.class");
			var inner_class = klass.InnerClasses.First ();

			Assert.True (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.True (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.False (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Public));
		}

		[Test]
		public void MakeInternalInterfacePackagePrivate ()
		{
			var klass = LoadClassFile ("InternalInterface.class");
			var inner_class = klass.InnerClasses.First ();

			Assert.True (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.True (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			// "package-private" is denoted as no visibility flags
			Assert.False (klass.AccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.False (klass.AccessFlags.HasFlag (ClassAccessFlags.Protected));
			Assert.False (klass.AccessFlags.HasFlag (ClassAccessFlags.Private));

			Assert.False (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Public));
			Assert.False (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Protected));
			Assert.False (inner_class.InnerClassAccessFlags.HasFlag (ClassAccessFlags.Private));
		}

		[Test]
		public void HideInternalConstructor ()
		{
			var klass = LoadClassFile ("InternalConstructor.class");
			var ctor = klass.Methods.First (m => m.Name == "<init>");

			Assert.True (ctor.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (ctor.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[Test]
		public void HideDefaultConstructorMarker ()
		{
			var klass = LoadClassFile ("DefaultConstructor.class");

			// init ()
			var ctor_0p = klass.Methods.Single (m => m.Name == "<init>" && m.GetParameters ().Length == 0);

			// init (string name)
			var ctor_1p = klass.Methods.Single (m => m.Name == "<init>" && m.GetParameters ().Length == 1);

			// init (string p0, int p1, DefaultConstructorMarker p2)
			var ctor_3p = klass.Methods.Single (m => m.Name == "<init>" && m.GetParameters ().Length == 3);

			Assert.True (ctor_3p.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			// Assert that the normal constructors are still public
			Assert.True (ctor_0p.AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.True (ctor_1p.AccessFlags.HasFlag (MethodAccessFlags.Public));

			// Assert that the synthetic "DefaultConstructorMarker" constructor has been marked private
			Assert.False (ctor_3p.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[Test]
		public void HideImplementationMethod ()
		{
			var klass = LoadClassFile ("MethodImplementation.class");
			var method = klass.Methods.First (m => m.Name == "toString-impl");

			Assert.True (method.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (method.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[Test]
		public void RenameExtensionParameter ()
		{
			var klass = LoadClassFile ("RenameExtensionParameterKt.class");
			var method = klass.Methods.First (m => m.Name == "toUtf8String");
			var p = method.GetParameters () [0];

			Assert.AreEqual ("$this$toUtf8String", p.Name);

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("obj", p.Name);
		}

		[Test]
		public void HideInternalMethod ()
		{
			var klass = LoadClassFile ("InternalMethod.class");
			var method = klass.Methods.First (m => m.Name == "take$main");

			Assert.True (method.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (method.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[Test]
		public void ParameterName ()
		{
			var klass = LoadClassFile ("ParameterName.class");
			var method = klass.Methods.First (m => m.Name == "take");
			var p = method.GetParameters () [0];

			Assert.AreEqual ("p0", p.Name);

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("count", p.Name);
		}

		[Test]
		public void HideInternalProperty ()
		{
			var klass = LoadClassFile ("InternalProperty.class");
			var getter = klass.Methods.First (m => m.Name == "getCity$main");
			var setter = klass.Methods.First (m => m.Name == "setCity$main");

			Assert.True (getter.AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.True (setter.AccessFlags.HasFlag (MethodAccessFlags.Public));

			KotlinFixups.Fixup (new [] { klass });

			Assert.False (getter.AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.False (setter.AccessFlags.HasFlag (MethodAccessFlags.Public));
		}

		[Test]
		public void RenameSetterParameter ()
		{
			var klass = LoadClassFile ("SetterParameterName.class");
			var setter = klass.Methods.First (m => m.Name == "setCity");
			var p = setter.GetParameters () [0];

			Assert.AreEqual ("p0", p.Name);

			KotlinFixups.Fixup (new [] { klass });

			Assert.AreEqual ("value", p.Name);
		}

		[Test]
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
			Assert.Null (uintarrayarray_method.GetParameters () [0].KotlinType);
			Assert.Null (uintarrayarray_method.KotlinReturnType);
		}

		[Test]
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

		[Test]
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

		[Test]
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

		[Test]
		public void HandleKotlinNameShadowing ()
		{
			var klass = LoadClassFile ("NameShadowing.class");

			KotlinFixups.Fixup (new [] { klass });

			Assert.True (klass.Methods.Single (m => m.Name == "count").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.True (klass.Methods.Single (m => m.Name == "hitCount").AccessFlags.HasFlag (MethodAccessFlags.Public));
			Assert.True (klass.Methods.Single (m => m.Name == "setType").AccessFlags.HasFlag (MethodAccessFlags.Public));
		}
	}
}
