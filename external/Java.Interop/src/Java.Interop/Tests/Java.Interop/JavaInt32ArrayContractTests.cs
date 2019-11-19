using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using NUnit.Framework;

namespace Java.InteropTests
{
	[TestFixture]
	public class JavaInt32ArrayContractTests : JavaPrimitiveArrayContract<JavaInt32Array, int>
	{
		protected override ICollection<int> CreateCollection (IEnumerable<int> values)
		{
			return new JavaInt32Array (values);
		}

		protected override ICollection<int> CreateCollection (IList<int> values)
		{
			return new JavaInt32Array (values);
		}

		protected override ICollection<int> CreateCollection (int length)
		{
			return new JavaInt32Array (length);
		}
	}
}

