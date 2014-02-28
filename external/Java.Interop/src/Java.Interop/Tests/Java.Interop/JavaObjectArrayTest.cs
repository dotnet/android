using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	public abstract class JavaObjectArrayContractTest<T> : JavaArrayContract<T>
	{
		protected override System.Collections.Generic.ICollection<T> CreateCollection (System.Collections.Generic.IEnumerable<T> values)
		{
			return new JavaObjectArray<T> (values);
		}

		[Test]
		public void Constructor_Exceptions ()
		{
			Assert.Throws<ArgumentNullException> (() => new JavaObjectArray<T> ((IEnumerable<T>)null));
			Assert.Throws<ArgumentNullException> (() => new JavaObjectArray<T> ((IList<T>)null));
			Assert.Throws<ArgumentException> (() => new JavaObjectArray<T> (-1));
		}
	}

	[TestFixture]
	public class JavaObjectArrayContractTest : JavaObjectArrayContractTest<JavaObject> {
		protected override JavaObject CreateValueA () {return new JavaObject ();}
		protected override JavaObject CreateValueB () {return new JavaObject ();}
		protected override JavaObject CreateValueC () {return new JavaObject ();}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new JavaObject [0]);
			Assert.AreEqual ("[Ljava/lang/Object;", ((IJavaObject) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestFixture]
	public class JavaObjectArray_Int32_ContractTest : JavaObjectArrayContractTest<int> {
		protected override int  CreateValueA () {return 1;}
		protected override int  CreateValueB () {return 2;}
		protected override int  CreateValueC () {return 3;}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new int[0]);
			Assert.AreEqual ("[Ljava/lang/Integer;", ((IJavaObject) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestFixture]
	public class JavaObjectArray_string_ContractTest : JavaObjectArrayContractTest<string> {
		protected override string CreateValueA () {return "a";}
		protected override string CreateValueB () {return "b";}
		protected override string CreateValueC () {return "c";}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new string[0]);
			Assert.AreEqual ("[Ljava/lang/String;", ((IJavaObject) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestFixture]
	public class JavaObjectArray_object_ContractTest : JavaObjectArrayContractTest<object> {
		protected override object CreateValueA () {return new object ();}
		protected override object CreateValueB () {return new object ();}
		protected override object CreateValueC () {return new object ();}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new object[0]);
			Assert.AreEqual ("[Ljava/lang/Object;", ((IJavaObject) c).GetJniTypeName ());
			Dispose (c);
		}
	}
}

