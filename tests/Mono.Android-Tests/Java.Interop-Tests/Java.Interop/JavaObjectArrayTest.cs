using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	public abstract class JavaObjectArrayContractTest<
			[DynamicallyAccessedMembers (DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors)]
			T
		> : JavaArrayContract<T>
	{
		protected override System.Collections.Generic.ICollection<T> CreateCollection (System.Collections.Generic.IEnumerable<T> values)
		{
			return new JavaObjectArray<T> (values);
		}

		[TestMethod]
		public void Constructor_Exceptions ()
		{
			Assert.ThrowsExactly<ArgumentNullException> (() => new JavaObjectArray<T> ((IEnumerable<T>)null));
			Assert.ThrowsExactly<ArgumentNullException> (() => new JavaObjectArray<T> ((IList<T>)null));
			Assert.ThrowsExactly<ArgumentException> (() => new JavaObjectArray<T> (-1));
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

		int beforeTestGrefCount;
		public TestContext TestContext { get; set; }

		[TestInitialize]
		public void LogBeginCurrentTestGrefCount ()
		{
			beforeTestGrefCount = JniEnvironment.Runtime.GlobalReferenceCount;
			JniEnvironment.Runtime.ObjectReferenceManager.WriteGlobalReferenceLine(
					"{0}",
					$"Begin {TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}; " +
					$"GREFs={beforeTestGrefCount}; "
			);
		}

		[TestCleanup]
		public void LogEndCurrentTestToGrefCount ()
		{
			int afterTestGrefCount  = JniEnvironment.Runtime.GlobalReferenceCount;
			JniEnvironment.Runtime.ObjectReferenceManager.WriteGlobalReferenceLine(
					"{0}",
					$"End {TestContext.FullyQualifiedTestClassName}.{TestContext.TestName}; " +
					$"GREFs={afterTestGrefCount}; diff={afterTestGrefCount - beforeTestGrefCount}"
			);
		}
	}

	[TestClass]
	public class JavaObjectArrayContractTest : JavaObjectArrayContractTest<JavaObject> {
		protected override JavaObject CreateValueA () {return new JavaObject ();}
		protected override JavaObject CreateValueB () {return new JavaObject ();}

		[TestMethod]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new JavaObject [0]);
			Assert.AreEqual ("[Ljava/lang/Object;", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestClass]
	public class JavaObjectArray_Int32_ContractTest : JavaObjectArrayContractTest<int> {
		protected override int  CreateValueA () {return 1;}
		protected override int  CreateValueB () {return 2;}

		[TestMethod]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new int[0]);
			Assert.AreEqual ("[Ljava/lang/Integer;", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestClass]
	public class JavaObjectArray_NullableByte_ContractTest : JavaObjectArrayContractTest<byte?> {
		protected override byte? CreateValueA () {return 128;}
		protected override byte? CreateValueB () {return 255;}

		[TestMethod]
		public void NullableByteValues ()
		{
			var values = new byte? [] { 1, null, 127, 128, 200, 255 };
			using (var array = new JavaObjectArray<byte?> (values)) {
				Assert.AreEqual ("[Ljava/lang/Byte;", array.GetJniTypeName ());
				CollectionAssert.AreEqual (values, array.ToArray ());
			}
		}
	}

	[TestClass]
	public class JavaObjectArray_Int32Array_ContractTest : JavaObjectArrayContractTest<int[]> {
		protected override int[]  CreateValueA () {return new[]{1};}
		protected override int[]  CreateValueB () {return new[]{2};}

		[TestMethod]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new int[0][]);
			Assert.AreEqual ("[[I", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestClass]
	public class JavaObjectArray_Int32ArrayArray_ContractTest : JavaObjectArrayContractTest<int[][]> {
		protected override int[][]  CreateValueA () {return new[]{new[]{1}};}
		protected override int[][]  CreateValueB () {return new[]{new[]{2}};}

		[TestMethod]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new int[0][][]);
			Assert.AreEqual ("[[[I", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestClass]
	public class JavaObjectArray_JavaInt32Array_ContractTest : JavaObjectArrayContractTest<JavaInt32Array> {
		protected override JavaInt32Array CreateValueA () {return new JavaInt32Array (new[]{1});}
		protected override JavaInt32Array CreateValueB () {return new JavaInt32Array (new[]{2});}

		[TestMethod]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new JavaInt32Array [0]);
			Assert.AreEqual ("[[I", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

	[TestClass]
	public class JavaObjectArray_string_ContractTest : JavaObjectArrayContractTest<string> {
		protected override string CreateValueA () {return "a";}
		protected override string CreateValueB () {return "b";}

		[TestMethod]
		public void ObjectArrayType ()
		{
			var c = CreateCollection (new string[0]);
			Assert.AreEqual ("[Ljava/lang/String;", ((IJavaPeerable) c).GetJniTypeName ());
			Dispose (c);
		}
	}

}
