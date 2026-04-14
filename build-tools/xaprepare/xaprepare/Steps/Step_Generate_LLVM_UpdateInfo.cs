using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;

namespace Xamarin.Android.Prepare;

class Step_Generate_LLVM_UpdateInfo : Step
{
	static ReadOnlySpan<byte> Utf8Bom => new byte[] { 0xEF, 0xBB, 0xBF };
	static readonly byte[] BidPropertyName = Encoding.UTF8.GetBytes ("bid");

	public Step_Generate_LLVM_UpdateInfo ()
		: base ("Generating LLVM source update information")
	{}

#pragma warning disable CS1998
	protected override async Task<bool> Execute (Context context)
	{
		try {
			if (!Generate (context)) {
				Log.WarningLine ("Failed to generate LLVM update info. Attempt to update LLVM sources may fail.");
			}
		} catch (Exception ex) {
			Log.WarningLine ($"Failed to generate LLVM update info. {ex.Message}");
			Log.DebugLine ($"Exception was thrown while generating LLVM update info.");
			Log.DebugLine (ex.ToString ());
		}

		// This step isn't critical, we never fail.
		return true;
	}
#pragma warning restore CS1998

	bool Generate (Context context)
	{
		// BUILD_INFO is a JSON document with build information, we need the "bid" component from there as it forms
		// part of the toolchain manifest name
		string? bid = GetBid (Path.Combine (Configurables.Paths.AndroidToolchainRootDirectory, "BUILD_INFO"));
		if (String.IsNullOrEmpty (bid)) {
			Log.DebugLine ("Unable to find LLVM toolchain bid information.");
			return false;
		}

		// Manifest contains GIT revisions of various NDK components. We need the LLVM project's one from there.
		string toolchainManifestPath = Path.Combine (Configurables.Paths.AndroidToolchainRootDirectory, $"manifest_{bid}.xml");
		(string? llvmProjectPath, string? llvmProjectRevision) = GetLlvmProjectInfo (toolchainManifestPath);

		if (String.IsNullOrEmpty (llvmProjectPath)) {
			Log.DebugLine ("Failed to read LLVM project path from the manifest.");
			return false;
		}

		if (String.IsNullOrEmpty (llvmProjectRevision)) {
			Log.DebugLine ("Failed to read LLVM project GIT revision from the manifest.");
			return false;
		}

		string? llvmProjectVersion = null;
		string androidVersionPath = Path.Combine (Configurables.Paths.AndroidToolchainRootDirectory, "AndroidVersion.txt");
		if (Path.Exists (androidVersionPath)) {
			try {
				foreach (string line in File.ReadLines (androidVersionPath)) {
					// In NDK r29 LLVM version was on the first line
					llvmProjectVersion = line.Trim ();
					break;
				}
			} catch (Exception ex) {
				Log.DebugLine ($"Failed to read LLVM Android version file '{androidVersionPath}'");
				Log.DebugLine ("Exception was thrown:");
				Log.DebugLine (ex.ToString ());
			}
		} else {
			Log.WarningLine ($"LLVM Android version file not found at {androidVersionPath}");
		}

		if (String.IsNullOrEmpty (llvmProjectVersion)) {
			llvmProjectVersion = "<unknown>";
		}

		Log.InfoLine ("LLVM project path: ", llvmProjectPath);
		Log.InfoLine ("LLVM project revision: ", llvmProjectRevision);
		Log.InfoLine ("LLVM project version: ", llvmProjectVersion);

		// Manifest uses https://googleplex-android.googlesource.com/ which is not accessible for mere mortals,
		// therefore we need to use the public URL
		var baseURIBuilder = new UriBuilder (Configurables.Urls.GoogleSourcesBase);
		baseURIBuilder.Path = $"{llvmProjectPath}/+/{llvmProjectRevision}";
		Uri baseURI = baseURIBuilder.Uri;

		const string updateSourcesInputName = "LlvmUpdateInfo.cs.in";
		string updateInfoSourceInputPath = Path.Combine (Configurables.Paths.BuildToolsScriptsDir, updateSourcesInputName);
		string updateInfoSourceOutputPath = Path.Combine (Configurables.Paths.BuildBinDir, Path.GetFileNameWithoutExtension (updateSourcesInputName));

		Log.InfoLine ();
		Log.InfoLine ($"Generating LLVM update info sources.");
		var updateInfoSource = new GeneratedPlaceholdersFile (
			new Dictionary <string, string> (StringComparer.Ordinal) {
				{ "@LLVM_PROJECT_BASE_URL@", baseURI.ToString () },
				{ "@LLVM_PROJECT_REVISION@", llvmProjectRevision },
				{ "@LLVM_PROJECT_VERSION@", llvmProjectVersion },
			},
			updateInfoSourceInputPath,
			updateInfoSourceOutputPath
		);
		updateInfoSource.Generate (context);

		return true;
	}

	(string? path, string? revision) GetLlvmProjectInfo (string manifestPath)
	{
		Log.DebugLine ($"Reading LLVM toolchain manifest from '{manifestPath}'");

		if (!File.Exists (manifestPath)) {
			Log.DebugLine ($"NDK LLVM manifest '{manifestPath}' not found");
			return (null, null);
		}

		var readerSettings = new XmlReaderSettings {
			ValidationType = ValidationType.None,
			DtdProcessing = DtdProcessing.Ignore,
			IgnoreWhitespace = true,
			IgnoreComments = true,
			IgnoreProcessingInstructions = true,
		};
		using var reader = XmlReader.Create (manifestPath, readerSettings);
		var doc = new XmlDocument ();
		doc.Load (reader);

		XmlNode? llvmToolchain = doc.SelectSingleNode ("//manifest/project[@name='toolchain/llvm-project']");
		if (llvmToolchain == null) {
			Log.DebugLine ("Failed to find LLVM toolchain info in the manifest.");
			return (null, null);
		}

		if (llvmToolchain.Attributes == null) {
			Log.DebugLine ("Unable to read path and revision info about the LLVM toolchain, no attributes on the element.");
			return (null, null);
		}

		XmlAttribute? path = llvmToolchain.Attributes["path"];
		XmlAttribute? revision = llvmToolchain.Attributes["revision"];

		return (path?.Value, revision?.Value);
	}

	string? GetBid (string buildInfoPath)
	{
		Log.DebugLine ($"Reading LLVM toolchain build info from '{buildInfoPath}'");

		ReadOnlySpan<byte> manifestBytes = File.ReadAllBytes (buildInfoPath);

		if (manifestBytes.StartsWith (Utf8Bom)) {
			manifestBytes = manifestBytes.Slice (Utf8Bom.Length);
		}

		string? bid = null;
		var reader = new Utf8JsonReader (manifestBytes);
		while (reader.Read ()) {
			if (reader.TokenType != JsonTokenType.PropertyName) {
				continue;
			}

			if (!reader.ValueTextEquals (BidPropertyName)) {
				continue;
			}

			// let's assume the manifest document is formatted correctly
			reader.Read ();
			if (reader.TokenType != JsonTokenType.String) {
				Log.DebugLine ($"Invalid token type '{reader.TokenType}' for the 'bid' property in LLVM manifest.");
				return null;
			}

			bid = reader.GetString ();
			break;
		}

		return bid;
	}
}
