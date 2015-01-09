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
		public void Frobnicate ()
		{
			dynamic d = new DynamicJavaClass ();

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
	}
}

