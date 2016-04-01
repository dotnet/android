using System;
using MonoDroid.Generation;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public class Adjuster
	{
		public void Process (string inputXmlFile, GenBase [] gens, string outputXmlFile)
		{
			var api = new JavaApi ();
			api.LoadReferences (gens);
			api.Load (inputXmlFile);
			api.Resolve ();
			api.CreateGenericInheritanceMapping ();
			api.MarkOverrides ();
			api.FindDefects ();
			api.Save (outputXmlFile);
		}
	}
}
