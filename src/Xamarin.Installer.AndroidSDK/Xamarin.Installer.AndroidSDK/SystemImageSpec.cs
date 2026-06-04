using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Xamarin.Installer.AndroidSDK.Common;

namespace Xamarin.Installer.AndroidSDK
{
	/// <summary>
	/// Specification of a single Android SDK System Image
	/// </summary>
	public class SystemImageSpec
	{
		/// <summary>
		/// ABI of the system image
		/// </summary>
		public AndroidSystemImageAbi ABI { get; set; }

		/// <summary>
		/// Tag (flavor) of the System Image (e.g. 'default', 'google_apis' etc - the list is
		/// not defined anywhere, the only source of information is the Android SDK itself, namely
		/// the 'system-images' directory off the Android SDK installation root. Subdirectories of
		/// that directory correspond to the Android SDK platforms and their subdirectories, in turn,
		/// are the names of the tags you can use here.
		/// </summary>
		public PackageTag Tag { get; set; }
	}
}
