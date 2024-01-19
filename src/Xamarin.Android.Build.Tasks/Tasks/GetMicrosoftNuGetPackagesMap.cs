#nullable enable

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using System.Net.Http;

namespace Xamarin.Android.Tasks;

public class GetMicrosoftNuGetPackagesMap : AndroidAsyncTask
{
	public override string TaskPrefix => "GNP";

	/// <summary>
	/// The cache directory to use for Maven artifacts.
	/// </summary>
	[Required]
	public string MavenCacheDirectory { get; set; } = null!; // NRT enforced by [Required]

	[Output]
	public string? ResolvedPackageMap { get; set; }

	public async override System.Threading.Tasks.Task RunTaskAsync ()
	{
		// TODO: We should check the age of the existing file and only download if it's too old
		var outfile = Path.Combine (MavenCacheDirectory, "microsoft-packages.json");

		if (File.Exists (outfile)) {
			ResolvedPackageMap = outfile;
			return;
		}

		// File missing, download new one
		try {
			var http = new HttpClient ();
			var json = await http.GetStringAsync ("https://aka.ms/ms-nuget-packages");

			File.WriteAllText (outfile, json);
			ResolvedPackageMap = outfile;
		} catch (Exception ex) {
			Log.LogMessage ("Could not download microsoft-packages.json: {0}", ex);
		}
	}
}

