#nullable enable

using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// For .NET 5 projects, this parses $(RuntimeIdentifer) (or RID) into $(AndroidSupportedAbis)
	/// 
	/// NOTE: .NET 5 does not currently support multiple RIDs, so this will need to be modified once that is available.
	/// </summary>
	public class RuntimeIdentifierToAbi : AndroidTask
	{
		public override string TaskPrefix => "RIAB";

		public string RuntimeIdentifier { get; set; } = "";

		public string [] RuntimeIdentifiers { get; set; } = [];

		[Output]
		public string SupportedAbis { get; set; } = "";

		public override bool RunTask ()
		{
			if (!string.IsNullOrEmpty (SupportedAbis)) {
				Log.LogCodedWarning ("XA0036", Properties.Resources.XA0036);
			}
			if (RuntimeIdentifiers != null && RuntimeIdentifiers.Length > 0) {
				SupportedAbis = string.Join (";", RuntimeIdentifiers.Select (rid => {
					var abi = AndroidRidAbiHelper.RuntimeIdentifierToAbi (rid);
					if (string.IsNullOrEmpty (abi))
						Log.LogCodedError ("XA0035", Properties.Resources.XA0035, rid);
					return abi;
				}));
			} else if (!string.IsNullOrEmpty (RuntimeIdentifier)) {
				SupportedAbis = AndroidRidAbiHelper.RuntimeIdentifierToAbi (RuntimeIdentifier);
				if (string.IsNullOrEmpty (SupportedAbis))
					Log.LogCodedError ("XA0035", Properties.Resources.XA0035, RuntimeIdentifier);
			} else if (string.IsNullOrEmpty (SupportedAbis)) {
				Log.LogCodedError ("XA0035", Properties.Resources.XA0035, "");
			}
			return !Log.HasLoggedErrors;
		}
	}
}
