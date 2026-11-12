using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.SourceWriter;

namespace generatortests.SourceWriters
{
	[TestClassAttribute]
	public class SourceWritersTestBase
	{
		protected string GetOutput (ISourceWriter writer)
		{
			var sw = new StringWriter ();
			var cw = new CodeWriter (sw);

			writer.Write (cw);

			return sw.ToString ();
		}
	}
}
