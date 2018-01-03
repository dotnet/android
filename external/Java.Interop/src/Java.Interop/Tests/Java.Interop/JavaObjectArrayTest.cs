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

		protected override int IndexOf (T[] array, T value)
		{
			for (int i = 0; i < array.Length; ++i)
				if (JniMarshal.RecursiveEquals (array [i], value))
					return i;
			return -1;
		}

		protected override bool SequenceEqual (IEnumerable<T> a, IEnumerable<T> b)
		{
			return JniMarshal.RecursiveEquals (a, b);
		}
	}

	[TestFixture]
	public class JavaObjectArrayContractTest : JavaObjectArrayContractTest<JavaObject> {
		protected override JavaObject CreateValueA () {return new JavaObject ();}
		protected override JavaObject CreateValueB () {return new JavaObject ();}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new JavaObject [0]);
			Assert.AreEqual ("[Ljava/lang/Object;", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestFixture]
	public class JavaObjectArray_Int32_ContractTest : JavaObjectArrayContractTest<int> {
		protected override int  CreateValueA () {return 1;}
		protected override int  CreateValueB () {return 2;}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new int[0]);
			Assert.AreEqual ("[Ljava/lang/Integer;", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestFixture]
	public class JavaObjectArray_Int32Array_ContractTest : JavaObjectArrayContractTest<int[]> {
		protected override int[]  CreateValueA () {return new[]{1};}
		protected override int[]  CreateValueB () {return new[]{2};}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new int[0][]);
			Assert.AreEqual ("[[I", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestFixture]
	public class JavaObjectArray_Int32ArrayArray_ContractTest : JavaObjectArrayContractTest<int[][]> {
		protected override int[][]  CreateValueA () {return new[]{new[]{1}};}
		protected override int[][]  CreateValueB () {return new[]{new[]{2}};}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new int[0][][]);
			Assert.AreEqual ("[[[I", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestFixture]
	public class JavaObjectArray_JavaInt32Array_ContractTest : JavaObjectArrayContractTest<JavaInt32Array> {
		protected override JavaInt32Array CreateValueA () {return new JavaInt32Array (new[]{1});}
		protected override JavaInt32Array CreateValueB () {return new JavaInt32Array (new[]{2});}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new JavaInt32Array [0]);
			Assert.AreEqual ("[[I", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestFixture]
	public class JavaObjectArray_string_ContractTest : JavaObjectArrayContractTest<string> {
		protected override string CreateValueA () {return "a";}
		protected override string CreateValueB () {return "b";}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new string[0]);
			Assert.AreEqual ("[Ljava/lang/String;", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestFixture]
	public class JavaObjectArray_object_ContractTest : JavaObjectArrayContractTest<object> {
		static  readonly    object  a   = new object ();

		protected override object CreateValueA () {return a;}
		protected override object CreateValueB () {return 42;}

		int grefStartCount;

		[TestFixtureSetUp]
		public void BeginCheckGlobalRefCount ()
		{
			// So that the JavaProxyObject.TypeRef GREF isn't counted.
			using (var o = new JavaObjectArray<object> (1))
				o [0] = a;
			grefStartCount  = JniEnvironment.Runtime.GlobalReferenceCount;
		}

		[TestFixtureTearDown]
		public void EndCheckGlobalRefCount ()
		{
			int gref    = JniEnvironment.Runtime.GlobalReferenceCount;
			Assert.IsTrue (gref <= (grefStartCount),
					string.Format ("JNI global references: grefStartCount={0}; gref={1}", grefStartCount, gref));
			JniEnvironment.Runtime.ValueManager.CollectPeers ();
		}

		[Test]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new object[0]);
			Assert.AreEqual ("[Ljava/lang/Object;", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}
}

