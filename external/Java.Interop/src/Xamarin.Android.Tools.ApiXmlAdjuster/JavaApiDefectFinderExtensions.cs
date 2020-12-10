using System;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiDefectFinderExtensions
	{
		public static void FindDefects (this JavaApi api)
		{
			foreach (var type in api.AllPackages.SelectMany (p => p.AllTypes).Where (t => !t.IsReferenceOnly))
				type.FindDefects ();
		}
		
		static void FindDefects (this JavaType type)
		{
			foreach (var m in type.Members.OfType<JavaMethodBase> ())
				m.FindParametersDefects ();
		}
		
		static void FindParametersDefects (this JavaMethodBase methodBase)
		{
			int dummy;
			foreach (var p in methodBase.Parameters) {
				if ((p.Name?.StartsWith ("p", StringComparison.Ordinal) ?? false) &&
						int.TryParse (p.Name.Substring (1), out dummy)) {
					Log.LogWarning ("Warning: {0} in {1} has 'unnamed' parameters", methodBase.Parent, methodBase);
					break; // reporting once is enough.
				}
			}
		}
	}
}

