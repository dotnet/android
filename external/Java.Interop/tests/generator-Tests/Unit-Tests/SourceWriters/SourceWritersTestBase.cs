using System;
using System.IO;
using NUnit.Framework;
using Xamarin.SourceWriter;

namespace generatortests.SourceWriters
{
	[TestFixture]
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
