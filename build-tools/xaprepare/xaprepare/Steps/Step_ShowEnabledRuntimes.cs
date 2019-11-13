using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Xamarin.Android.Prepare
{
	class Step_ShowEnabledRuntimes : Step
	{
		public Step_ShowEnabledRuntimes ()
			: base ("Configured targets")
		{
		}

		protected override Task<bool> Execute (Context context)
		{
			List<Runtime> enabledRuntimes = MonoRuntimesHelpers.GetEnabledRuntimes (new Runtimes (), enableLogging: true);
			if (enabledRuntimes.Count == 0)
				Log.StatusLine ("No runtimes to build/install");

			return Task.FromResult (true);
		}
	}
}
