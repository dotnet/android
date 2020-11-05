using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiNonBindableStripper
	{
		public static void StripNonBindables (this JavaApi api)
		{
			var invalids = new List<JavaMember> ();
			foreach (var member in api.Packages.SelectMany (p => p.Types)
			         .SelectMany (t => t.Members).Where (m => m.Name != null && m.Name.Contains ('$')))
				invalids.Add (member);
			foreach (var invalid in invalids)
				invalid.Parent?.Members.Remove (invalid);
		}
	}
}
