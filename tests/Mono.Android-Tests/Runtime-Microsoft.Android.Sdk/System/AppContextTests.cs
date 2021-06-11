using NUnit.Framework;
using System;

namespace SystemTests
{
	[TestFixture]
	public class AppContextTests
	{
		static readonly object [] GetDataSource = new object [] {
			new object [] {
				/* name */     "test_bool",
				/* expected */ "true",
			},
			new object [] {
				/* name */     "test_integer",
				/* expected */ "42",
			},
			new object [] {
				/* name */     "test_string",
				/* expected */ "foo",
			},
		};

		[Test]
		[TestCaseSource (nameof (GetDataSource))]
		public void GetData (string name, string expected)
		{
			Assert.AreEqual (expected, AppContext.GetData (name));
		}
	}
}
