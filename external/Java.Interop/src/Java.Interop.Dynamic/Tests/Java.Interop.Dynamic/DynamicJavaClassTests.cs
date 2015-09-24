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

	class MyDynamicObject : DynamicJavaClass {

		public MyDynamicObject ()
			: base ("java/lang/Object")
		{
		}

		public  int     MyProperty {get; set;}

		public int Method (string value)
		{
			return value.Length;
		}
	}

	[TestFixture]
	class DynamicJavaClassTests : Java.InteropTests.JavaVMFixture
	{
		const   string  Arrays_class    = "java/util/Arrays";
		const   string  Integer_class   = "java/lang/Integer";

		[Test]
		public void Constructor ()
		{
			Assert.Throws<ArgumentNullException> (() => new DynamicJavaClass (null));
		}

		[Test]
		public void DisposedInstanceThrowsObjectDisposedException ()
		{
			dynamic Integer = new DynamicJavaClass (Integer_class);
			Integer.Dispose ();
			Integer.Dispose ();  // Dispose() is idempotent
			Assert.Catch<Exception> (() => Integer.bitCount (2));
			Assert.Catch<Exception> (() => {
				int max = Integer.MAX_INT;
			});
			Assert.Catch<Exception> (() => {
				Integer.MAX_INT = 42;
			});
		}

		[Test]
		public void JniClassName ()
		{
			dynamic Arrays  = new DynamicJavaClass (Arrays_class);
			Assert.AreEqual (Arrays_class, Arrays.JniClassName);
			Arrays.Dispose ();
		}

		[Test]
		public void CallStaticMethod ()
		{
			dynamic Arrays  = new DynamicJavaClass (Arrays_class);
			var array       = new int[]{ 1, 2, 3, 4 };
			int value       = 3;
			int index       = Arrays.binarySearch (array, value);
			Assert.AreEqual (2, index);
			Arrays.Dispose ();
		}

		[Test]
		public void ReadStaticMember ()
		{
			dynamic Integer = new DynamicJavaClass (Integer_class);
			int max = Integer.MAX_VALUE;
			Assert.AreEqual (int.MaxValue, max);
			Integer.Dispose ();
		}

		[Test]
		public void WriteStaticMember ()
		{
			dynamic Integer = new DynamicJavaClass (Integer_class);
			int cur = Integer.MAX_VALUE;
			Console.WriteLine ("# MAX_VALUE={0}", cur);
			Integer.MAX_VALUE = 42;
			int max = Integer.MAX_VALUE;
			Console.WriteLine ("# set MAX_VALUE=42");
			Assert.AreEqual (42, max);
			Integer.MAX_VALUE   = cur;
			Console.WriteLine ("# done!");
			Integer.Dispose ();
		}

		[Test]
		public void FallbackPropertySet ()
		{
			dynamic d = new MyDynamicObject ();

			d.MyProperty    = 42;
			int v           = d.MyProperty;
			Assert.AreEqual (42, v);
			d.Dispose ();
		}

		[Test]
		public void FallbackInvokeMember ()
		{
			dynamic d   = new MyDynamicObject ();
			int v       = d.Method ("foo");
			Assert.AreEqual (3, v);
			d.Dispose ();
		}
	}
}

