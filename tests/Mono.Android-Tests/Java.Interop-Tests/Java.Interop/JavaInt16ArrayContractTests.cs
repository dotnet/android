using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JavaInt16ArrayContractTests : JavaPrimitiveArrayContract<JavaInt16Array, short>
	{
		protected override ICollection<short> CreateCollection (IEnumerable<short> values)
		{
			return new JavaInt16Array (values);
		}

		protected override ICollection<short> CreateCollection (IList<short> values)
		{
			return new JavaInt16Array (values);
		}

		protected override ICollection<short> CreateCollection (int length)
		{
			return new JavaInt16Array (length);
		}
	}
}

