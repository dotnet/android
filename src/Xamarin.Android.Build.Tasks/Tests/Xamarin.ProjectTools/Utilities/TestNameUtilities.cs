using System.IO;
using System.Linq;

namespace Xamarin.ProjectTools
{
	public static class TestNameUtilities
	{
		static readonly char [] InvalidChars = ['{', '}', '(', ')', '$', ':', ';', '"', '\'', ',', '=', '|'];

		public static string GetDirectoryName (string testName)
		{
			foreach (var c in InvalidChars.Concat (Path.GetInvalidPathChars ()).Concat (Path.GetInvalidFileNameChars ())) {
				testName = testName.Replace (c, '_');
			}
			return testName.Replace ("_", "");
		}
	}
}
