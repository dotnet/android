#nullable enable

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Android.Build.Tasks;
using System.Net.Http;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tasks;

public class GetMicrosoftNuGetPackagesMap : AndroidAsyncTask
{
	static readonly HttpClient http_client = new HttpClient ();

	public override string TaskPrefix => "GNP";

	/// <summary>
	/// The cache directory to use for Maven artifacts.
	/// </summary>
	[Required]
	public string MavenCacheDirectory { get; set; } = null!; // NRT enforced by [Required]

	[Output]
	public string? ResolvedPackageMap { get; set; }

	public override async System.Threading.Tasks.Task RunTaskAsync ()
	{
		Directory.CreateDirectory (MavenCacheDirectory);

		// We're going to store the resolved package map in the cache directory as
		// "microsoft-packages-{YYYYMMDD}.json". If the file is older than today,
		// we'll try to download a new one.
		var all_files = PackagesFile.FindAll (MavenCacheDirectory);

		if (!all_files.Any (x => x.IsToday)) {
			// No file for today, download a new one
			try {
				var json = await http_client.GetStringAsync ("https://aka.ms/ms-nuget-packages");
				var outfile = Path.Combine (MavenCacheDirectory, $"microsoft-packages-{DateTime.Today:yyyyMMdd}.json");

				File.WriteAllText (outfile, json);

				if (PackagesFile.TryParse (outfile, out var packagesFile))
					all_files.Insert (0, packagesFile);  // Sorted so this one is first

			} catch (Exception ex) {
				Log.LogMessage ("Could not download microsoft-packages.json: {0}", ex.Message);
			}
		}

		// Delete all files but the latest
		foreach (var file in all_files.Skip (1)) {
			try {
				File.Delete (Path.Combine (MavenCacheDirectory, file.FileName));
			} catch {
				// Ignore exceptions
			}
		}

		ResolvedPackageMap = all_files.FirstOrDefault ()?.FileName;
	}
}

class PackagesFile
{
	public string FileName { get; }
	public DateTime DownloadDate { get; }
	public bool IsToday => DownloadDate == DateTime.Today;

	PackagesFile (string filename, DateTime downloadDate)
	{
		FileName = filename;
		DownloadDate = downloadDate;
	}

	public static List<PackagesFile> FindAll (string directory)
	{
		var files = new List<PackagesFile> ();

		foreach (var file in Directory.GetFiles (directory, "microsoft-packages-*.json")) {
			if (TryParse (file, out var packagesFile))
				files.Add (packagesFile);
		}

		files.OrderByDescending (x => x.DownloadDate);

		return files;
	}

	public static bool TryParse (string filepath, [NotNullWhen (true)]out PackagesFile? file)
	{
		file = default;

		var filename = Path.GetFileNameWithoutExtension (filepath);

		if (!filename.StartsWith ("microsoft-packages-", StringComparison.OrdinalIgnoreCase))
			return false;

		var date = filename.Substring ("microsoft-packages-".Length);

		if (!DateTime.TryParseExact (date, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var downloadDate))
			return false;

		file = new PackagesFile (filepath, downloadDate);

		return true;
	}
}

