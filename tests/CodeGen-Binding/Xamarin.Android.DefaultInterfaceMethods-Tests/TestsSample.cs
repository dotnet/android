using System;
using Com.Xamarin.Test;
using NUnit.Framework;

namespace Xamarin.Android.DefaultInterfaceMethodsTests
{
	[TestFixture]
	public class DimTest
	{
		[Test]
		public void TestCSharp8DefaultInterfaceMethods ()
		{
			// C# 8 syntax is awkward.
			// If you use "var" then it is declared as the class, and fails to resolve.
			// Those default interface methods are only callable via interface!
			IDefaultInterfaceMethods c = new ImplementedClass ();
			Assert.AreEqual (0, c.Foo (), "#1");
			Assert.AreEqual (1, c.Bar, "#2");
			Assert.AreEqual (-1, c.ToImplement (), "#3");
		}
	}
}
