using System;
using System.Json;
using System.Net.Http;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public sealed class GitHubStatus : Task
	{
		[Required]
		public                  string      Repository       { get; set; }

		[Required]
		public                  string      Context          { get; set; }

		[Required]
		public                  string      CommitHash       { get; set; }

		[Output]
		public                  string      TargetUrl        { get; set; }

		[Output]
		public                  string      Description      { get; set; }

		public GitHubStatus ()
		{
		}

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (GitHubStatus)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Repository)}: {Repository}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Context)}: {Context}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (CommitHash)}: {CommitHash}");

			using (var client = new HttpClient ())
			{
				client.DefaultRequestHeaders.Add("User-Agent", "xamarin-android");

				var statusUri = $"https://api.github.com/repos/{Repository}/status/{CommitHash}";
				Log.LogMessage (MessageImportance.Normal, $"Fetching `{statusUri}`.");

				using (var response = client.GetAsync(statusUri, HttpCompletionOption.ResponseContentRead).Result)
				{
					response.EnsureSuccessStatusCode ();

					var content = response.Content.ReadAsStringAsync ().Result;
					var json = JsonValue.Parse (content);

					var found = false;

					foreach (var status in (JsonArray)json["statuses"])
					{
						string context = status["context"], state = status["state"],
							description = status["description"], targetUrl = status["target_url"];

						Log.LogMessage (MessageImportance.Low, $"  {context} [{state}]: {description}, {targetUrl}");

						if (context != Context)
						{
							continue;
						}

						found = true;
						TargetUrl = targetUrl;
						Description = description;

						if (state != "success")
						{
							Log.LogError ($"\"{Context}\" state is not \"success\", it is \"{state}\"");
						}

						break;
					}

					if (!found)
					{
						Log.LogError ("Unable to find a \"PKG-mono\" on commit");
					}
				}
			}

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (TargetUrl)}: {TargetUrl}");
			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Description)}: {Description}");

			return !Log.HasLoggedErrors;
		}
	}
}

