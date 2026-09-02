#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Build.Framework;

using Microsoft.Android.Build.Tasks;

namespace Xamarin.Android.Tasks;

public sealed class GenerateR8JniManifestProguardConfiguration : AndroidTask
{
	static readonly XNamespace AndroidNamespace = "http://schemas.android.com/apk/res/android";

	public override string TaskPrefix => "GRJMPC";

	[Required]
	public string AndroidManifestFile { get; set; } = "";

	[Required]
	public string OutputFile { get; set; } = "";

	public override bool RunTask ()
	{
		XDocument manifest;
		try {
			manifest = XDocument.Load (AndroidManifestFile, LoadOptions.None);
		} catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is XmlException) {
			LogR8JniMappingError (string.Format (Properties.Resources.XA4327_ManifestReadFailure, AndroidManifestFile, ex.Message));
			return false;
		}

		XElement? root = manifest.Root;
		string? packageName = root?.Attribute ("package")?.Value;
		if (root?.Name.LocalName != "manifest" || packageName.IsNullOrWhiteSpace ()) {
			LogR8JniMappingError (string.Format (Properties.Resources.XA4327_ManifestPackageMissing, AndroidManifestFile));
			return false;
		}

		var classes = new SortedSet<string> (StringComparer.Ordinal);
		foreach (XElement element in root.DescendantsAndSelf ()) {
			switch (element.Name.LocalName) {
				case "application":
					AddClass (classes, packageName, element, "name");
					AddClass (classes, packageName, element, "backupAgent");
					AddClass (classes, packageName, element, "appComponentFactory");
					AddClass (classes, packageName, element, "zygotePreloadName");
					break;
				case "activity":
				case "service":
				case "receiver":
				case "provider":
				case "instrumentation":
				case "process":
					AddClass (classes, packageName, element, "name");
					break;
				case "activity-alias":
					AddClass (classes, packageName, element, "targetActivity");
					break;
			}
		}

		string content = string.Join ("\n", classes.Select (name => $"-keep class {name} {{ <init>(); }}"));
		if (content.Length > 0) {
			content += "\n";
		}
		File.WriteAllText (OutputFile, content, Files.UTF8withoutBOM);
		return !Log.HasLoggedErrors;
	}

	static void AddClass (ISet<string> classes, string packageName, XElement element, string attributeName)
	{
		string? value = element.Attribute (AndroidNamespace + attributeName)?.Value;
		if (value.IsNullOrWhiteSpace () || value [0] == '@' || value [0] == '?') {
			return;
		}
		if (value [0] == '.') {
			classes.Add (packageName + value);
		} else if (value.IndexOf ('.') < 0) {
			classes.Add (packageName + "." + value);
		} else {
			classes.Add (value);
		}
	}

	void LogR8JniMappingError (string detail) =>
		Log.LogCodedError ("XA4327", Properties.Resources.XA4327, detail);
}
