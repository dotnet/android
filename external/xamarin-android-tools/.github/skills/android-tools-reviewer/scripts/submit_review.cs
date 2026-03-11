// Usage: dotnet run submit_review.cs <owner> <repo> <pr_number> <review_json_path>
//
// Validates the review JSON structure, then submits it as a batched PR review
// via `gh api`. Requires the `gh` CLI to be installed and authenticated.

using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

if (args.Length != 4)
{
	Console.Error.WriteLine ($"Usage: dotnet run submit_review.cs <owner> <repo> <pr_number> <review_json_path>");
	return 1;
}

var owner = args [0];
var repo = args [1];
var prNumber = args [2];
var jsonPath = args [3];

if (!File.Exists (jsonPath)) {
	Console.Error.WriteLine ($"❌ File not found: {jsonPath}");
	return 1;
}

var json = File.ReadAllText (jsonPath);
JsonDocument doc;
try {
	doc = JsonDocument.Parse (json);
} catch (JsonException ex) {
	Console.Error.WriteLine ($"❌ Invalid JSON: {ex.Message}");
	return 1;
}

using (doc) {

var root = doc.RootElement;

// Validate structure
var errors = new System.Collections.Generic.List<string> ();

if (!root.TryGetProperty ("event", out var eventProp) || eventProp.ValueKind != JsonValueKind.String) {
	errors.Add ("Missing or invalid 'event' field — must be COMMENT, APPROVE, or REQUEST_CHANGES");
} else {
	var ev = eventProp.GetString () ?? "";
	if (ev != "COMMENT" && ev != "APPROVE" && ev != "REQUEST_CHANGES")
		errors.Add ($"Invalid event '{ev}' — must be COMMENT, APPROVE, or REQUEST_CHANGES");
}

if (!root.TryGetProperty ("body", out var bodyProp) || bodyProp.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace (bodyProp.GetString ())) {
	errors.Add ("Missing or empty 'body' field (the review summary)");
}

if (root.TryGetProperty ("comments", out var commentsProp)) {
	if (commentsProp.ValueKind != JsonValueKind.Array) {
		errors.Add ("Invalid 'comments' field — must be an array");
	} else {
		int i = 0;
		foreach (var c in commentsProp.EnumerateArray ()) {
			var prefix = $"comments[{i}]";

			if (!c.TryGetProperty ("path", out var pathProp) || pathProp.ValueKind != JsonValueKind.String || string.IsNullOrEmpty (pathProp.GetString ()))
				errors.Add ($"{prefix}: missing 'path'");

			if (!c.TryGetProperty ("line", out var lineProp) || lineProp.ValueKind != JsonValueKind.Number || lineProp.GetInt32 () < 1)
				errors.Add ($"{prefix}: 'line' must be a positive integer");

			if (!c.TryGetProperty ("body", out var cbody) || cbody.ValueKind != JsonValueKind.String) {
				errors.Add ($"{prefix}: missing or empty 'body'");
			} else {
				var commentBody = cbody.GetString () ?? "";
				if (string.IsNullOrWhiteSpace (commentBody))
					errors.Add ($"{prefix}: missing or empty 'body'");
				else if (!commentBody.StartsWith ("🤖"))
					errors.Add ($"{prefix}: body must start with 🤖 prefix");
			}

			if (c.TryGetProperty ("side", out var sideProp) && sideProp.ValueKind == JsonValueKind.String) {
				var side = sideProp.GetString () ?? "";
				if (side != "LEFT" && side != "RIGHT")
					errors.Add ($"{prefix}: 'side' must be LEFT or RIGHT, got '{side}'");
			}

			i++;
		}
	}
}

if (errors.Count > 0) {
	Console.Error.WriteLine ("❌ Review JSON validation failed:");
	foreach (var e in errors)
		Console.Error.WriteLine ($"  • {e}");
	return 1;
}

var commentCount = root.TryGetProperty ("comments", out var cp) && cp.ValueKind == JsonValueKind.Array ? cp.GetArrayLength () : 0;
Console.WriteLine ($"✅ Review validated: {commentCount} comment(s)");
Console.WriteLine ($"📤 Submitting review to {owner}/{repo}#{prNumber}...");

// Submit via gh api
var psi = new ProcessStartInfo {
	FileName = "gh",
	UseShellExecute = false,
	RedirectStandardOutput = true,
	RedirectStandardError = true,
};
psi.ArgumentList.Add ("api");
psi.ArgumentList.Add ($"repos/{owner}/{repo}/pulls/{prNumber}/reviews");
psi.ArgumentList.Add ("--method");
psi.ArgumentList.Add ("POST");
psi.ArgumentList.Add ("--input");
psi.ArgumentList.Add (jsonPath);

using var process = Process.Start (psi);
if (process is null) {
	Console.Error.WriteLine ("❌ Failed to start 'gh' — is it installed and on PATH?");
	return 1;
}
var stdoutTask = process.StandardOutput.ReadToEndAsync ();
var stderrTask = process.StandardError.ReadToEndAsync ();
process.WaitForExit ();
var stdout = stdoutTask.Result;
var stderr = stderrTask.Result;

if (process.ExitCode != 0) {
	Console.Error.WriteLine ($"❌ gh api failed (exit code {process.ExitCode}):");
	if (!string.IsNullOrEmpty (stderr))
		Console.Error.WriteLine (stderr);
	if (!string.IsNullOrEmpty (stdout)) {
		try {
			using var errDoc = JsonDocument.Parse (stdout);
			if (errDoc.RootElement.TryGetProperty ("message", out var msg))
				Console.Error.WriteLine ($"  GitHub says: {msg.GetString ()}");
		} catch (JsonException) {
			Console.Error.WriteLine (stdout);
		}
	}
	return 1;
}

Console.WriteLine ("✅ Review posted.");
try {
	using var resp = JsonDocument.Parse (stdout);
	if (resp.RootElement.TryGetProperty ("html_url", out var url))
		Console.WriteLine ($"   {url.GetString ()}");
} catch (JsonException) {
	Console.WriteLine ("Note: API response was not JSON; review was posted but URL is unavailable.");
}

} // using (doc)

return 0;
