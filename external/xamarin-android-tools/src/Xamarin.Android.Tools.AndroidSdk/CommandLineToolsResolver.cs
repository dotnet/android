// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace Xamarin.Android.Tools;

static class CommandLineToolsResolver
{
	const string PackageRevisionProperty = "Pkg.Revision";

	public static CommandLineTool? Find (
		string sdkPath,
		string toolName,
		string extension,
		Action<TraceLevel, string>? logger = null)
	{
		var cmdlineToolsDir = Path.Combine (sdkPath, "cmdline-tools");
		if (Directory.Exists (cmdlineToolsDir)) {
			var candidates = FindCandidates (cmdlineToolsDir, toolName, extension, logger);
			if (candidates.Count > 0) {
				candidates.Sort (CompareCandidates);
				var selected = candidates [0];
				logger?.Invoke (
					TraceLevel.Verbose,
					$"Selected '{selected.Tool.Path}' from command-line tools revision '{selected.Tool.Revision ?? "unknown"}'.");
				return selected.Tool;
			}
		}

		return null;
	}

	internal static bool TryParseRevision (string? value, out ParsedRevision revision)
	{
		revision = default;
		if (value is null)
			return false;

		var text = value.Trim ();
		if (text.Length == 0)
			return false;
		var suffixIndex = -1;
		for (var i = 0; i < text.Length; i++) {
			if (text [i] == '-' || char.IsWhiteSpace (text [i])) {
				suffixIndex = i;
				break;
			}
		}

		var versionText = suffixIndex >= 0 ? text.Substring (0, suffixIndex) : text;
		var prerelease = suffixIndex >= 0 ? text.Substring (suffixIndex + 1).Trim () : null;
		if (suffixIndex >= 0 && string.IsNullOrEmpty (prerelease))
			return false;

		if (!Version.TryParse (versionText, out var parsedVersion))
			return false;

		revision = new ParsedRevision (parsedVersion, prerelease);
		return true;
	}

	static List<Candidate> FindCandidates (
		string cmdlineToolsDir,
		string toolName,
		string extension,
		Action<TraceLevel, string>? logger)
	{
		var candidates = new List<Candidate> ();
		try {
			foreach (var directory in Directory.GetDirectories (cmdlineToolsDir)) {
				var directoryName = Path.GetFileName (directory);
				if (string.IsNullOrEmpty (directoryName))
					continue;

				var toolPath = Path.Combine (directory, "bin", toolName + extension);
				if (!File.Exists (toolPath))
					continue;

				string? revisionText = null;
				var revision = default (ParsedRevision?);
				var revisionFromSource = false;

				if (TryReadPackageRevision (directory, logger, out var packageRevision)) {
					if (TryParseRevision (packageRevision, out var parsedRevision)) {
						revisionText = packageRevision;
						revision = parsedRevision;
						revisionFromSource = true;
					} else {
						logger?.Invoke (
							TraceLevel.Warning,
							$"Ignoring invalid {PackageRevisionProperty} in '{Path.Combine (directory, "source.properties")}': '{packageRevision}'.");
					}
				}

				if (revision is null && TryParseRevision (directoryName, out var directoryRevision)) {
					revisionText = directoryName;
					revision = directoryRevision;
				}

				candidates.Add (new Candidate (
					directoryName,
					new CommandLineTool (toolPath, revisionText),
					revision,
					revisionFromSource));
			}
		} catch (IOException ex) {
			logger?.Invoke (TraceLevel.Warning, $"Could not enumerate '{cmdlineToolsDir}': {ex.Message}");
		} catch (UnauthorizedAccessException ex) {
			logger?.Invoke (TraceLevel.Warning, $"Could not enumerate '{cmdlineToolsDir}': {ex.Message}");
		}

		return candidates;
	}

	static bool TryReadPackageRevision (
		string directory,
		Action<TraceLevel, string>? logger,
		out string? revision)
	{
		revision = null;
		var sourceProperties = Path.Combine (directory, "source.properties");
		try {
			return SourceProperties.TryGetProperty (sourceProperties, PackageRevisionProperty, out revision);
		} catch (IOException ex) {
			logger?.Invoke (TraceLevel.Warning, $"Could not read '{sourceProperties}': {ex.Message}");
		} catch (UnauthorizedAccessException ex) {
			logger?.Invoke (TraceLevel.Warning, $"Could not read '{sourceProperties}': {ex.Message}");
		}

		return false;
	}

	static int CompareCandidates (Candidate first, Candidate second)
	{
		if (first.Revision.HasValue != second.Revision.HasValue)
			return first.Revision.HasValue ? -1 : 1;

		if (first.Revision is ParsedRevision firstRevision && second.Revision is ParsedRevision secondRevision) {
			var revisionComparison = secondRevision.CompareTo (firstRevision);
			if (revisionComparison != 0)
				return revisionComparison;
		}

		if (first.RevisionFromSource != second.RevisionFromSource)
			return first.RevisionFromSource ? -1 : 1;

		var firstIsLatest = string.Equals (first.DirectoryName, "latest", StringComparison.Ordinal);
		var secondIsLatest = string.Equals (second.DirectoryName, "latest", StringComparison.Ordinal);
		if (firstIsLatest != secondIsLatest)
			return firstIsLatest ? -1 : 1;

		return string.Compare (first.DirectoryName, second.DirectoryName, StringComparison.Ordinal);
	}

	sealed class Candidate
	{
		public string DirectoryName { get; }
		public CommandLineTool Tool { get; }
		public ParsedRevision? Revision { get; }
		public bool RevisionFromSource { get; }

		public Candidate (
			string directoryName,
			CommandLineTool tool,
			ParsedRevision? revision,
			bool revisionFromSource)
		{
			DirectoryName = directoryName;
			Tool = tool;
			Revision = revision;
			RevisionFromSource = revisionFromSource;
		}
	}

	internal readonly struct ParsedRevision : IComparable<ParsedRevision>
	{
		public Version Version { get; }
		public string? Prerelease { get; }

		public ParsedRevision (Version version, string? prerelease)
		{
			Version = version;
			Prerelease = prerelease;
		}

		public int CompareTo (ParsedRevision other)
		{
			var versionComparison = Version.CompareTo (other.Version);
			if (versionComparison != 0)
				return versionComparison;

			var isPrerelease = !string.IsNullOrEmpty (Prerelease);
			var otherIsPrerelease = !string.IsNullOrEmpty (other.Prerelease);
			if (isPrerelease != otherIsPrerelease)
				return isPrerelease ? -1 : 1;

			return ComparePrerelease (Prerelease, other.Prerelease);
		}

		static int ComparePrerelease (string? first, string? second)
		{
			if (first is null || second is null)
				return string.Compare (first, second, StringComparison.OrdinalIgnoreCase);

			var firstNumberStart = FindTrailingNumberStart (first);
			var secondNumberStart = FindTrailingNumberStart (second);
			if (firstNumberStart < first.Length && secondNumberStart < second.Length) {
				var firstLabel = first.Substring (0, firstNumberStart);
				var secondLabel = second.Substring (0, secondNumberStart);
				if (string.Equals (firstLabel, secondLabel, StringComparison.OrdinalIgnoreCase) &&
					long.TryParse (first.Substring (firstNumberStart), out var firstNumber) &&
					long.TryParse (second.Substring (secondNumberStart), out var secondNumber))
					return firstNumber.CompareTo (secondNumber);
			}

			return string.Compare (first, second, StringComparison.OrdinalIgnoreCase);
		}

		static int FindTrailingNumberStart (string value)
		{
			var index = value.Length;
			while (index > 0 && char.IsDigit (value [index - 1]))
				index--;
			return index;
		}
	}
}
