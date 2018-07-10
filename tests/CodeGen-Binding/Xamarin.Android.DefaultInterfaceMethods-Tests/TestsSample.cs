using System;
using NUnit.Framework;

namespace Xamarin.Android.DefaultInterfaceMethodsTests
{
	[TestFixture]
	public class TestsSample
	{

		[SetUp]
		public void Setup () { }


		[TearDown]
		public void Tear () { }

		[Test]
		public void Pass ()
		{
			var c = new Com.Xamarin.Test.ImplementedClass ();
			Assert.AreEqual (-1, c.ToImplement (), "#1");
			Assert.AreEqual (0, c.Foo (), "#2");
			Assert.AreEqual (1, c.Bar, "#3");
		}
	}
}
