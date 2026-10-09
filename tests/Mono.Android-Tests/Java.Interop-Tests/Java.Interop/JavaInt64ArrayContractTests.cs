using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaInt64ArrayContractTests : JavaPrimitiveArrayContract<JavaInt64Array, long>
	{
		protected override ICollection<long> CreateCollection (IEnumerable<long> values)
		{
			return new JavaInt64Array (values);
		}

		protected override ICollection<long> CreateCollection (IList<long> values)
		{
			return new JavaInt64Array (values);
		}

		protected override ICollection<long> CreateCollection (int length)
		{
			return new JavaInt64Array (length);
		}

		protected override long GetElement (JniArrayElements elements, int index)
		{
			return ((JniInt64ArrayElements) elements) [index];
		}

		[Test]
		[Category ("JniPrimitiveArrayBounds")]
		public void GetElements_LargeArray ()
		{
			const int length = 268435456;
			var elements = CreateLargeLease (length);
			// The fake pointer is only used to obtain a ref; the test never dereferences it.
			Assert.DoesNotThrow (() => GetFirstElement (elements));
		}

		static unsafe JniInt64ArrayElements CreateLargeLease (int length)
		{
			return new JniInt64ArrayElements (default, (long*) 1, length);
		}

		static ref long GetFirstElement (JniInt64ArrayElements elements)
		{
			return ref elements [0];
		}
	}
}
