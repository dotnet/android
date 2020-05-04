using System;
using Microsoft.Build.Framework;

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

		public string RuntimeIdentifier { get; set; }

		[Output]
		public string SupportedAbis { get; set; }

		public override bool RunTask ()
		{
			if (!string.IsNullOrEmpty (RuntimeIdentifier)) {
				SupportedAbis = MonoAndroidHelper.RuntimeIdentifierToAbi (RuntimeIdentifier);
			}
			if (string.IsNullOrEmpty (SupportedAbis)) {
				// Default to a single, default ABI if blank
				SupportedAbis = "arm64-v8a";
			}
			return !Log.HasLoggedErrors;
		}
	}
}
