using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JavaSByteArrayContractTests : JavaPrimitiveArrayContract<JavaSByteArray, sbyte>
	{
		protected override ICollection<sbyte> CreateCollection (IEnumerable<sbyte> values)
		{
			return new JavaSByteArray (values);
		}

		protected override ICollection<sbyte> CreateCollection (IList<sbyte> values)
		{
			return new JavaSByteArray (values);
		}

		protected override ICollection<sbyte> CreateCollection (int length)
		{
			return new JavaSByteArray (length);
		}
	}
}

