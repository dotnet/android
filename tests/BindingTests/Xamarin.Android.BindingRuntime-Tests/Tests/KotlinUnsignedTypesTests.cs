using System;
using Com.Xamarin.Android;
using Foo;
using Java.Lang;
using NUnit.Framework;

namespace Xamarin.Android.BindingRuntime_Tests
{
	[TestFixture]
	public class KotlinUnsignedTypesTests
	{
		[Test]
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

		[Test]
		public void TestUnsignedArrayTypeMembers ()
		{
			var foo = new Foo.UnsignedInstanceMethods ();

			var uint_array = new uint [] { 1u, 2u, uint.MaxValue };
			var ushort_array = new ushort [] { 1, 2, ushort.MaxValue };
			var ulong_array = new ulong [] { 1u, 2u, ulong.MaxValue };
			var ubyte_array = new byte [] { 1, 2, byte.MaxValue };

			Assert.AreEqual (uint_array, foo.UintArrayInstanceMethod (uint_array));
			Assert.AreEqual (ushort_array, foo.UshortArrayInstanceMethod (ushort_array));
			Assert.AreEqual (ulong_array, foo.UlongArrayInstanceMethod (ulong_array));
			Assert.AreEqual (ubyte_array, foo.UbyteArrayInstanceMethod (ubyte_array));
		}

		[Test]
		public void TestUnsignedTypeInterfaceImplementedMembers ()
		{
			var foo = new Foo.UnsignedInterfaceImplementedMethods ();

			var uint_array = new uint [] { 1u, 2u, uint.MaxValue };

			Assert.AreEqual (uint.MaxValue, foo.UnsignedInterfaceMethod (uint.MaxValue));

			Assert.AreEqual (3u, foo.UnsignedInterfaceProperty);
			foo.UnsignedInterfaceProperty = uint.MaxValue;
			Assert.AreEqual (uint.MaxValue, foo.UnsignedInterfaceProperty);

			Assert.AreEqual (uint_array, foo.UnsignedArrayInterfaceMethod (uint_array));
		}

		[Test]
		public void TestUnsignedTypeAbstractImplementedMembers ()
		{
			var foo = new Foo.UnsignedAbstractImplementedMethods ();

			Assert.AreEqual (uint.MaxValue, foo.UnsignedAbstractMethod (uint.MaxValue));

			Assert.AreEqual (3u, foo.UnsignedAbstractClassProperty);
			foo.UnsignedAbstractClassProperty = uint.MaxValue;
			Assert.AreEqual (uint.MaxValue, foo.UnsignedAbstractClassProperty);
		}

		[Test]
		public void TestUnsignedConstant ()
		{
			Assert.AreEqual (3u, Foo.UnsignedMethodsKt.SubsystemDeprecated);
		}
	}
}
