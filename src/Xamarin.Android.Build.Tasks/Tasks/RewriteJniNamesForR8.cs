#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Rewrites JNI names embedded in compiled assemblies: Android.Runtime.RegisterAttribute,
	/// the Java.Interop.Jni*SignatureAttribute family, and generated JniPeerMembers /
	/// RegisterNatives <c>ldstr</c> strings. The R8 <c>mapping.txt</c> supplies the obfuscated
	/// Java names that managed metadata must reference.
	///
	/// Each assembly is fully reconstructed with System.Reflection.Metadata: every metadata table
	/// row is cloned in its original order, so every entity token keeps its value, while the
	/// heaps, method bodies, managed resources, and mapped field data are re-emitted. Replacements
	/// may therefore be of any length.
	///
	/// An adjacent PDB is copied unchanged: it stays valid because method tokens, IL offsets, and
	/// the PE's CodeView identity (GUID, age, path) are all preserved.
	/// </summary>
	public class RewriteJniNamesForR8 : AndroidTask
	{
		public override string TaskPrefix => "RJN";

		[Required]
		public ITaskItem [] SourceFiles { get; set; } = [];

		public ITaskItem [] DestinationFiles { get; set; } = [];

		public string? DestinationDirectory { get; set; }

		[Required]
		public string MappingFile { get; set; } = "";

		public string? RewriteManifestFile { get; set; }

		[Output]
		public ITaskItem [] RewrittenFiles { get; set; } = [];

		public override bool RunTask ()
		{
			RewrittenFiles = [];
			if (DestinationDirectory.IsNullOrEmpty () && SourceFiles.Length != DestinationFiles.Length) {
				Log.LogCodedError ("XA4325", Properties.Resources.XA4325, Properties.Resources.XA4325_SourceDestinationCount);
				return !Log.HasLoggedErrors;
			}

			R8Mapping mapping = R8Mapping.Load (MappingFile);
			var rewrittenFiles = new List<ITaskItem> (SourceFiles.Length);

			for (int i = 0; i < SourceFiles.Length; i++) {
				string source = SourceFiles [i].ItemSpec;
				string destination = DestinationDirectory.IsNullOrEmpty ()
					? DestinationFiles [i].ItemSpec
					: Path.Combine (DestinationDirectory, Path.GetFileName (source));
				try {
					RewriteAssembly (source, destination, mapping);
					var rewritten = new TaskItem (SourceFiles [i]) {
						ItemSpec = destination,
					};
					rewritten.SetMetadata ("OriginalItemSpec", source);
					rewrittenFiles.Add (rewritten);
				} catch (JniRewriteException e) {
					Log.LogCodedError ("XA4325", Properties.Resources.XA4325,
						string.Format (Properties.Resources.XA4325_AssemblyFailure, source, e.Message));
				}
			}

			if (!Log.HasLoggedErrors) {
				RewrittenFiles = rewrittenFiles.ToArray ();
				if (!RewriteManifestFile.IsNullOrEmpty ()) {
					WriteRewriteManifest (RewriteManifestFile, mapping.AccessedEntries);
				}
			}
			return !Log.HasLoggedErrors;
		}

		static void WriteRewriteManifest (string path, IEnumerable<string> entries)
		{
			string? directory = Path.GetDirectoryName (path);
			if (!directory.IsNullOrEmpty ()) {
				Directory.CreateDirectory (directory);
			}
			Files.CopyIfStringChanged (R8Mapping.CreateManifestContent (entries), path);
		}

		void RewriteAssembly (string sourcePath, string destinationPath, R8Mapping mapping)
		{
			string? destinationDirectory = Path.GetDirectoryName (destinationPath);
			if (!destinationDirectory.IsNullOrEmpty ()) {
				Directory.CreateDirectory (destinationDirectory);
			}

			JniRewriteResult result = JniAssemblyRewriter.Rewrite (File.ReadAllBytes (sourcePath), mapping, Log);

			Log.LogDebugMessage ($"RewriteJniNamesForR8: rewrote {result.ReplacementCount} JNI name(s) in '{Path.GetFileName (sourcePath)}'.");
			if (result.StrongNameSignatureCleared) {
				Log.LogDebugMessage ($"RewriteJniNamesForR8: '{Path.GetFileName (sourcePath)}' is strong-named; preserved its public-key identity and emitted a delay-signed linker input.");
			}

			bool inPlace = String.Equals (Path.GetFullPath (sourcePath), Path.GetFullPath (destinationPath), StringComparison.Ordinal);
			if (!inPlace || result.ReplacementCount != 0) {
				using var output = new MemoryStream (result.Image, writable: false);
				Files.CopyIfStreamChanged (output, destinationPath);
			}
			if (!inPlace) {
				CopyAdjacentPdbUnchanged (sourcePath, destinationPath);
			}
		}

		static void CopyAdjacentPdbUnchanged (string sourcePath, string destinationPath)
		{
			string pdbSource = Path.ChangeExtension (sourcePath, "pdb");
			string pdbDestination = Path.ChangeExtension (destinationPath, "pdb");
			if (File.Exists (pdbSource)) {
				Files.CopyIfChanged (pdbSource, pdbDestination);
			} else if (File.Exists (pdbDestination)) {
				Files.SetWriteable (pdbDestination);
				File.Delete (pdbDestination);
			}
		}
	}
}
