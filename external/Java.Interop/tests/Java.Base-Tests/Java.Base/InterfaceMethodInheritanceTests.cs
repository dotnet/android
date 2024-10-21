using System;

using Java.Interop;

using NUnit.Framework;

namespace Java.BaseTests {

	[TestFixture]
	public class InterfaceMethodInheritanceTests : JavaVMFixture {

		[Test]
		public void InterfaceMethod ()
		{
			using var iface   = global::Net.Dot.Jni.Test.HasInterfaceMethodInheritance.Create ();
			var m = iface!.M ();
			Assert.AreEqual ("HasInterfaceMethodInheritance.m", m);
			var n = iface!.N ();
			Assert.AreEqual ("HasInterfaceMethodInheritance.n", n);
			var o = iface!.O ();
			Assert.AreEqual ("HasInterfaceMethodInheritance.o", o);
			var p = iface!.P ();
			Assert.AreEqual ("HasInterfaceMethodInheritance.p", p);
		}
	}
}
