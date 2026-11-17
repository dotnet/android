using System;
using System.Collections.Generic;
using System.Linq;

using Java.Interop;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Java.InteropTests
{
	[TestClass]
	public class JavaSingleArrayContractTests : JavaPrimitiveArrayContract<JavaSingleArray, float>
	{
		protected override ICollection<float> CreateCollection (IEnumerable<float> values)
		{
			return new JavaSingleArray (values);
		}

		protected override ICollection<float> CreateCollection (IList<float> values)
		{
			return new JavaSingleArray (values);
		}

		protected override ICollection<float> CreateCollection (int length)
		{
			return new JavaSingleArray (length);
		}
	}
}

