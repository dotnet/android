using System;
using System.IO;
using System.Threading.Tasks;

namespace Xamarin.Android.Prepare
{
	partial class Step_InstallOpenJDK
	{
		async Task<bool> Unpack (string fullArchivePath, string destinationDirectory, bool cleanDestinationBeforeUnpacking = false)
		{
			return await Utilities.Unpack (fullArchivePath, destinationDirectory, cleanDestinatioBeforeUnpacking: true);
		}
	}
}
