using System;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	class MainClass
	{
		public static void Main (string [] args)
		{
			var inputXmlFile    = args [0];
			var outputXmlFile   = args [1];

			var api = new JavaApi ();
			api.Load (inputXmlFile);
			api.Resolve ();
			api.CreateGenericInheritanceMapping ();
			api.MarkOverrides ();
			api.FindDefects ();
			api.Save (outputXmlFile);
		}
	}
}
