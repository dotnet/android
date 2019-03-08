using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	class Step_PrepareMSBuild : Step
	{
		public Step_PrepareMSBuild ()
			: base ("Preparing MSbuild")
		{}

#pragma warning disable CS1998
		protected override async Task<bool> Execute (Context context)
		{
			Log.StatusLine (".:! NOT IMPLEMENTED YET !:. (Possibly not needed?)");
			return true;
		}
#pragma warning restore CS1998
	}
}
