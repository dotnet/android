using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Java.Interop.Tools.Generator;
using MonoDroid.Generation;
using NUnit.Framework;

namespace generatortests
{
	public class ReportTests
	{
		[Test]
		public void FormatTests ()
		{
			var code = 0x37;
			var msg = "There was a {0} error";
			var args = "bad";
			var sourcefile = @"C:\code\test.cs";
			var line = 32;
			var col = 12;

			Assert.AreEqual ("error BG0037: There was a bad error", Report.Format (true, code, null, 0, 0, msg, args));
			Assert.AreEqual (@"C:\code\test.cs: error BG0037: There was a bad error", Report.Format (true, code, sourcefile, 0, 0, msg, args));
			Assert.AreEqual (@"C:\code\test.cs(32): error BG0037: There was a bad error", Report.Format (true, code, sourcefile, line, 0, msg, args));
			Assert.AreEqual (@"C:\code\test.cs(32, 12): error BG0037: There was a bad error", Report.Format (true, code, sourcefile, line, col, msg, args));
			Assert.AreEqual (@"C:\code\test.cs(32, 12): warning BG0037: There was a bad error", Report.Format (false, code, sourcefile, line, col, msg, args));
		}
	}
}
