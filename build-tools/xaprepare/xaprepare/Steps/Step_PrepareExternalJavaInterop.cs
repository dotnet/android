using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_PrepareExternalJavaInterop : Step
	{
		public Step_PrepareExternalJavaInterop ()
			: base ("Preparing external/Java.Interop")
		{}

		protected override async Task<bool> Execute (Context context)
		{
			return await ExecuteOSSpecific (context);
		}
	}
}
