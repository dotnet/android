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
	}
}

