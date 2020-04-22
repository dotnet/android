using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Provides a localizable way to log a warning from an MSBuild target.
	/// </summary>
	public class AndroidWarning : Task
	{
		/// <summary>
		/// Warning code
		/// </summary>
		[Required]
		public string Code { get; set; }

		/// <summary>
		/// The name of the resource from Properties\Resources.resx that contains the message
		/// </summary>
		[Required]
		public string ResourceName { get; set; }

		/// <summary>
		/// The string format arguments to use for any numbered format items in the resource provided by ResourceName
		/// </summary>
		public string [] FormatArguments { get; set; }

		public override bool Execute ()
		{
			Log.LogCodedWarning (
				Code,
				Properties.Resources.ResourceManager.GetString (ResourceName, Properties.Resources.Culture),
				FormatArguments
			);
			return true;
		}
	}
}
