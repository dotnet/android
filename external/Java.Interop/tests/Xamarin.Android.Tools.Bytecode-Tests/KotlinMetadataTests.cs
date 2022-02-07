using System;
using Xamarin.Android.Tools.Bytecode;
using NUnit.Framework;
using System.Linq;

namespace Xamarin.Android.Tools.BytecodeTests
{
	[TestFixture]
	public class KotlinMetadataTests : ClassFileFixture
	{
		[Test]
		public void PublicKotlinClassFile ()
		{
			var klass_meta = GetClassMetadata ("PublicClass.class");

			Assert.AreEqual (KotlinClassVisibility.Public, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);
		}

		[Test]
		public void PrivateKotlinClassFile ()
		{
			var klass_meta = GetClassMetadata ("PrivateClass.class");

			Assert.AreEqual (KotlinClassVisibility.Private, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);
		}

		[Test]
		public void InternalKotlinClassFile ()
		{
			var klass_meta = GetClassMetadata ("InternalClass.class");

			Assert.AreEqual (KotlinClassVisibility.Internal, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);
		}

		[Test]
		public void ProtectedKotlinClassFile ()
		{
			var klass_meta = GetClassMetadata ("PublicClass$ProtectedClass.class");

			Assert.AreEqual (KotlinClassVisibility.Protected, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);
		}

		[Test]
		public void SealedClassFile ()
		{
			var klass_meta = GetClassMetadata ("SealedClass.class");

			Assert.AreEqual (KotlinClassInheritability.Sealed, klass_meta.Inheritability);
		}

		[Test]
		public void Interface ()
		{
			var klass_meta = GetClassMetadata ("MyInterface.class");

			Assert.AreEqual (KotlinClassVisibility.Public, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassInheritability.Abstract, klass_meta.Inheritability);
			Assert.AreEqual (KotlinClassType.Interface, klass_meta.ObjectType);

			Assert.AreEqual (2, klass_meta.Functions.Count);
			Assert.AreEqual (2, klass_meta.Properties.Count);
		}

		[Test]
		public void InterfaceDefaultImpls ()
		{
			var meta = GetMetadataForClassFile ("MyInterface$DefaultImpls.class");

			Assert.AreEqual (KotlinMetadataKind.SyntheticClass, meta.Kind);

			// TODO: We don't support SyntheticClass yet
		}

		[Test]
		public void InterfaceInheritingInterface ()
		{
			var klass_meta = GetClassMetadata ("MyInterface2.class");

			Assert.AreEqual (KotlinClassVisibility.Public, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassInheritability.Abstract, klass_meta.Inheritability);
			Assert.AreEqual (KotlinClassType.Interface, klass_meta.ObjectType);

			Assert.AreEqual (0, klass_meta.Functions.Count);
			Assert.AreEqual (2, klass_meta.Properties.Count);
			Assert.AreEqual ("MyInterface;", klass_meta.SuperTypes [0].ClassName);
		}

		[Test]
		public void ClassInheritingInterface ()
		{
			var klass_meta = GetClassMetadata ("MyInterfaceChild.class");

			Assert.AreEqual (KotlinClassVisibility.Public, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);

			Assert.AreEqual (1, klass_meta.Functions.Count);
			Assert.AreEqual (1, klass_meta.Properties.Count);
			Assert.AreEqual ("MyInterface;", klass_meta.SuperTypes [0].ClassName);
		}

		[Test]
		public void DataClass ()
		{
			var klass_meta = GetClassMetadata ("DataClass.class");

			Assert.AreEqual (KotlinClassVisibility.Public, klass_meta.Visibility);
			Assert.True (klass_meta.Flags.HasFlag (KotlinClassFlags.IsData));
			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);

			Assert.AreEqual (1, klass_meta.Constructors.Count);
			Assert.AreEqual (6, klass_meta.Functions.Count);
			Assert.AreEqual (3, klass_meta.Properties.Count);
		}

		[Test]
		public void EnumClassFile ()
		{
			var klass_meta = GetClassMetadata ("EnumClass.class");

			Assert.AreEqual (KotlinClassVisibility.Public, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassType.EnumClass, klass_meta.ObjectType);
			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);

			Assert.AreEqual (1, klass_meta.Constructors.Count);
			Assert.AreEqual (4, klass_meta.EnumEntries.Count);
		}

		[Test]
		public void EnumClassWithInterfaces ()
		{
			var klass_meta = GetClassMetadata ("EnumClassWithInterfaces.class");

			Assert.AreEqual (KotlinClassVisibility.Public, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassType.EnumClass, klass_meta.ObjectType);
			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);

			Assert.AreEqual (1, klass_meta.Constructors.Count);
			Assert.AreEqual (1, klass_meta.Functions.Count);
			Assert.AreEqual (2, klass_meta.EnumEntries.Count);

			Assert.AreEqual ("PLUS", klass_meta.EnumEntries [0]);
			Assert.AreEqual ("TIMES", klass_meta.EnumEntries [1]);

			Assert.AreEqual (2, klass_meta.SuperTypes.Count);

			Assert.AreEqual ("kotlin/Enum", klass_meta.SuperTypes [0].ClassName);
			Assert.AreEqual ("EnumClassWithInterfacesInterface;", klass_meta.SuperTypes [1].ClassName);
		}

		[Test]
		public void EnumClassWithFunction ()
		{
			var klass_meta = GetClassMetadata ("EnumClassWithInterfaces$PLUS.class");

			Assert.AreEqual (KotlinClassVisibility.Public, klass_meta.Visibility);
			Assert.AreEqual (KotlinClassType.EnumEntry, klass_meta.ObjectType);
			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);

			Assert.AreEqual (1, klass_meta.Functions.Count);
			Assert.AreEqual (1, klass_meta.SuperTypes.Count);

			Assert.AreEqual ("EnumClassWithInterfaces;", klass_meta.SuperTypes [0].ClassName);
		}

		[Test]
		public void InlineClass ()
		{
			var klass_meta = GetClassMetadata ("InlineClass.class");

			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);
			Assert.True (klass_meta.Flags.HasFlag (KotlinClassFlags.IsInlineClass));
		}

		[Test]
		public void Object ()
		{
			var klass_meta = GetClassMetadata ("Object.class");

			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);
			Assert.AreEqual (KotlinClassType.Object, klass_meta.ObjectType);
		}

		[Test]
		public void CompanionObject ()
		{
			var klass_meta = GetClassMetadata ("CompanionObject$Companion.class");

			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);
			Assert.AreEqual (KotlinClassType.CompanionObject, klass_meta.ObjectType);
		}

		[Test]
		public void KotlinExtensionMethods ()
		{
			var klass_meta = GetClassMetadata ("ExtensionMethods.class");

			Assert.AreEqual (KotlinClassInheritability.Final, klass_meta.Inheritability);
			Assert.AreEqual (KotlinClassVisibility.Public, klass_meta.Visibility);
		}
	}
}

