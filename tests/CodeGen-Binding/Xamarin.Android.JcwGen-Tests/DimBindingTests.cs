using System;
using Com.Xamarin.Android;
using Java.Lang;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Xamarin.Android.JcwGenTests
{
	[TestClass]
	public class DimTest
	{
		[TestMethod]
		public void TestDefaultInterfaceMethods ()
		{
			var empty = new EmptyOverrideClass ();
			var iface = empty as IDefaultMethodsInterface;

			Assert.AreEqual (0, iface.Foo ());
			Assert.AreEqual (2, iface.Bar);
			global::Xamarin.Android.UnitTests.AndroidTestAssert.DoesNotThrow (() => iface.Bar = 5);

			Assert.AreEqual (0, iface.InvokeFoo ());

			Assert.ThrowsExactly<UnsupportedOperationException> (() => iface.ToImplement ());
		}

		[TestMethod]
		public void TestOverriddenDefaultInterfaceMethods ()
		{
			var over = new ImplementedOverrideClass ();
			var iface = over as IDefaultMethodsInterface;

			Assert.AreEqual (6, over.Foo ());
			Assert.AreEqual (100, over.Bar);
			global::Xamarin.Android.UnitTests.AndroidTestAssert.DoesNotThrow (() => over.Bar = 5);

			Assert.AreEqual (6, iface.InvokeFoo ());
		}

		[TestMethod]
		public void TestManagedEmptyDefaultInterfaceMethods ()
		{
			// Test using empty C# implementing interface
			var empty = new ManagedEmptyDefault ();
			var iface = empty as IDefaultMethodsInterface;

			Assert.AreEqual (0, iface.Foo ());

			Assert.AreEqual (0, iface.InvokeFoo ());
		}

		[TestMethod]
		public void TestManagedOverriddenDefaultInterfaceMethods ()
		{
			// Test using method overridden in C#
			var over = new ManagedOverrideDefault ();
			var iface = over as IDefaultMethodsInterface;

			Assert.AreEqual (15, over.Foo ());
			Assert.AreEqual (15, iface.Foo ());

			Assert.AreEqual (15, iface.InvokeFoo ());
		}

		[TestMethod]
		public void TestStaticMethods ()
		{
			Assert.AreEqual (10, IStaticMethodsInterface.Foo ());

			Assert.AreEqual (3, IStaticMethodsInterface.Value);
			global::Xamarin.Android.UnitTests.AndroidTestAssert.DoesNotThrow (() => IStaticMethodsInterface.Value = 5);
		}

		[TestMethod]
		public void TestChainedDefaultInterfaceMethods ()
		{
			var over = new ImplementedChainOverrideClass ();
			var iface = over as IDefaultMethodsInterface;

			Assert.AreEqual (6, over.Foo ());
			Assert.AreEqual (100, over.Bar);
			global::Xamarin.Android.UnitTests.AndroidTestAssert.DoesNotThrow (() => over.Bar = 5);

			Assert.AreEqual (6, iface.InvokeFoo ());
		}

		[TestMethod]
		public void TestStaticInterfaceMethods ()
		{
			Assert.AreEqual (0, IDefaultMethodsInterface.StaticFoo ());
		}

		class ManagedEmptyDefault : Java.Lang.Object, IDefaultMethodsInterface
		{
		}

		class ManagedOverrideDefault : Java.Lang.Object, IDefaultMethodsInterface
		{
			public int Foo () => 15;
		}
	}
}
