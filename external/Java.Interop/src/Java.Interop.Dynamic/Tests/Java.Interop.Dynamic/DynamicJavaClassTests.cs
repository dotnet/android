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
	class DynamicJavaClassTests : Java.InteropTests.JavaVMFixture
	{
		[Test]
		public void Constructor ()
		{
			Assert.Throws<ArgumentNullException> (() => new DynamicJavaClass (null));
		}

		[Test]
		public void CallStaticMethod ()
		{
			dynamic Arrays  = new DynamicJavaClass ("java/util/Arrays");
			var array       = new int[]{ 1, 2, 3, 4 };
			int value       = 3;
			int index       = Arrays.binarySearch (array, value);
			Assert.AreEqual (2, index);
		}

		[Test]
		public void ReadStaticMember ()
		{
			dynamic Integer = new DynamicJavaClass ("java/lang/Integer");
			int max = Integer.MAX_VALUE;
			Assert.AreEqual (int.MaxValue, max);
		}

		[Test]
		public void WriteStaticMember ()
		{
			dynamic Integer = new DynamicJavaClass ("java/lang/Integer");
			int cur = Integer.MAX_VALUE;
			Console.WriteLine ("# MAX_VALUE={0}", cur);
			Integer.MAX_VALUE = 42;
			int max = Integer.MAX_VALUE;
			Console.WriteLine ("# set MAX_VALUE=42");
			Assert.AreEqual (42, max);
			Integer.MAX_VALUE   = cur;
			Console.WriteLine ("# done!");
		}
	}
}

