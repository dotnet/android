using System;
using MonoDroid.Generation;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public class Adjuster
	{
		public void Process (string inputXmlFile, GenBase [] gens, string outputXmlFile, int reportVerbosity)
		{
			switch (reportVerbosity) {
			case 0:
				break;
			case 1:
				Log.Verbosity = Log.LoggingLevel.Error;
				break;
			case 2:
				Log.Verbosity = Log.LoggingLevel.Warning;
				break;
			default:
				Log.Verbosity = Log.LoggingLevel.Debug;
				break;
			}
			var api = new JavaApi ();
			api.LoadReferences (gens);
			api.Load (inputXmlFile);
			api.StripNonBindables ();
			api.Resolve ();
			api.CreateGenericInheritanceMapping ();
			api.MarkOverrides ();
			api.FindDefects ();
			api.Save (outputXmlFile);
		}
	}
}
