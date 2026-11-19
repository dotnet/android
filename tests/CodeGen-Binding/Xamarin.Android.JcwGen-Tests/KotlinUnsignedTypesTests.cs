using System;
using Com.Xamarin.Android;
using Foo;
using Java.Lang;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.JcwGenTests
{
	[TestClass]
	public class KotlinUnsignedTypesTests
	{
		[TestMethod]
		public void TestUnsignedTypeMembers ()
		{
			var foo = new Foo.UnsignedInstanceMethods ();

			Assert.AreEqual (uint.MaxValue, foo.UnsignedInstanceMethod (uint.MaxValue));

			Assert.AreEqual (3u, foo.UnsignedInstanceProperty);
			foo.UnsignedInstanceProperty = uint.MaxValue;
			Assert.AreEqual (uint.MaxValue, foo.UnsignedInstanceProperty);

			Assert.AreEqual (ushort.MaxValue, foo.UshortInstanceMethod (ushort.MaxValue));

			Assert.AreEqual (3u, foo.UshortInstanceProperty);
			foo.UshortInstanceProperty = ushort.MaxValue;
			Assert.AreEqual (ushort.MaxValue, foo.UshortInstanceProperty);

			Assert.AreEqual (ulong.MaxValue, foo.UlongInstanceMethod (ulong.MaxValue));

			Assert.AreEqual (3u, foo.UlongInstanceProperty);
			foo.UlongInstanceProperty = ulong.MaxValue;
			Assert.AreEqual (ulong.MaxValue, foo.UlongInstanceProperty);

			Assert.AreEqual (byte.MaxValue, foo.UbyteInstanceMethod (byte.MaxValue));

			Assert.AreEqual (3u, foo.UbyteInstanceProperty);
			foo.UbyteInstanceProperty = byte.MaxValue;
			Assert.AreEqual (byte.MaxValue, foo.UbyteInstanceProperty);
		}

		[TestMethod]
		public void TestUnsignedArrayTypeMembers ()
		{
			var foo = new Foo.UnsignedInstanceMethods ();

			var uint_array = new uint [] { 1u, 2u, uint.MaxValue };
			var ushort_array = new ushort [] { 1, 2, ushort.MaxValue };
			var ulong_array = new ulong [] { 1u, 2u, ulong.MaxValue };
			var ubyte_array = new byte [] { 1, 2, byte.MaxValue };

			CollectionAssert.AreEqual (uint_array, foo.UintArrayInstanceMethod (uint_array));
			CollectionAssert.AreEqual (ushort_array, foo.UshortArrayInstanceMethod (ushort_array));
			CollectionAssert.AreEqual (ulong_array, foo.UlongArrayInstanceMethod (ulong_array));
			CollectionAssert.AreEqual (ubyte_array, foo.UbyteArrayInstanceMethod (ubyte_array));
		}

		[TestMethod]
		public void TestUnsignedTypeInterfaceImplementedMembers ()
		{
			var foo = new Foo.UnsignedInterfaceImplementedMethods ();

			var uint_array = new uint [] { 1u, 2u, uint.MaxValue };

			Assert.AreEqual (uint.MaxValue, foo.UnsignedInterfaceMethod (uint.MaxValue));

			Assert.AreEqual (3u, foo.UnsignedInterfaceProperty);
			foo.UnsignedInterfaceProperty = uint.MaxValue;
			Assert.AreEqual (uint.MaxValue, foo.UnsignedInterfaceProperty);

			CollectionAssert.AreEqual (uint_array, foo.UnsignedArrayInterfaceMethod (uint_array));
		}

		[TestMethod]
		public void TestUnsignedTypeAbstractImplementedMembers ()
		{
			var foo = new Foo.UnsignedAbstractImplementedMethods ();

			Assert.AreEqual (uint.MaxValue, foo.UnsignedAbstractMethod (uint.MaxValue));

			Assert.AreEqual (3u, foo.UnsignedAbstractClassProperty);
			foo.UnsignedAbstractClassProperty = uint.MaxValue;
			Assert.AreEqual (uint.MaxValue, foo.UnsignedAbstractClassProperty);
		}

		[TestMethod]
		public void TestUnsignedConstant ()
		{
			Assert.AreEqual (3u, Foo.UnsignedMethodsKt.SubsystemDeprecated);
		}
	}
}
