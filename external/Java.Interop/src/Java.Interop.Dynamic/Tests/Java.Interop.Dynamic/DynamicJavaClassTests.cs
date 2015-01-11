using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;

using Java.Interop;
using Java.Interop.Dynamic;

using Mono.Linq.Expressions;

using NUnit.Framework;

namespace Java.Interop.DynamicTests {

	[TestFixture]
	class DynamicJavaClassTests : JVM
	{
		[Test]
		public void Constructor ()
		{
			Assert.Throws<ArgumentNullException> (() => new DynamicJavaClass (null));
		}

		[Test]
		public void Frobnicate ()
		{
			Console.WriteLine ("---");
			dynamic d = new DynamicJavaClass ("java/lang/Object");

			d.P3 = d.M1(d.P1, d.M2(d.P2));
			Console.WriteLine ("--");
			try {
				int a = d.member;
			} catch (Exception e) {
				Console.WriteLine (e);
			}
			Console.WriteLine ("--");
			try {
				int b = d.method(42);
			} catch (Exception e) {
				Console.WriteLine (e);
			}
		}

		[Test]
		public void AccessStaticMember ()
		{
			dynamic Integer = new DynamicJavaClass ("java/lang/Integer");
			int max = Integer.MAX_VALUE;
			Assert.AreEqual (int.MaxValue, max);
		}
	}
}

