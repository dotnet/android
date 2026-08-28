#nullable enable

using System;
using System.IO;
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Xamarin.Android.Tasks.JniRemapping;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Rewrites the JNI names embedded in compiled assemblies - Android.Runtime.RegisterAttribute,
	/// the Java.Interop.Jni*SignatureAttribute family, the JniPeerMembers / RegisterNatives
	/// <c>ldstr</c> strings the generator emits, and the null-terminated UTF-8 JNI data the
	/// trimmable typemap generator stores in FieldRVA - according to an R8 <c>mapping.txt</c>, so
	/// that typemap generation, ILLink, and ILC see names that already match the R8-obfuscated
	/// Java side.
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

		[Required]
		public ITaskItem [] DestinationFiles { get; set; } = [];

		[Required]
		public string MappingFile { get; set; } = "";

		public override bool RunTask ()
		{
			if (SourceFiles.Length != DestinationFiles.Length) {
				Log.LogCodedError ("RJN0000", "SourceFiles and DestinationFiles must contain the same number of items.");
				return !Log.HasLoggedErrors;
			}

			R8Mapping mapping = R8Mapping.Load (MappingFile);

			for (int i = 0; i < SourceFiles.Length; i++) {
				string source = SourceFiles [i].ItemSpec;
				try {
					RewriteAssembly (source, DestinationFiles [i].ItemSpec, mapping);
				} catch (JniRewriteException e) {
					Log.LogCodedError ("RJN0001", $"Could not rewrite the JNI names in '{source}': {e.Message}");
				}
			}

			return !Log.HasLoggedErrors;
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
				Log.LogCodedWarning ("RJN0002", $"'{Path.GetFileName (sourcePath)}' was strong-name signed; the rewritten assembly is left delay-signed (its signature directory space is preserved so it can be re-signed) because no signing key is available here.");
			}

			bool inPlace = String.Equals (Path.GetFullPath (sourcePath), Path.GetFullPath (destinationPath), StringComparison.Ordinal);
			if (!inPlace || result.ReplacementCount != 0) {
				File.WriteAllBytes (destinationPath, result.Image);
			}
			if (!inPlace) {
				CopyAdjacentPdbUnchanged (sourcePath, destinationPath);
			}
		}

		static void CopyAdjacentPdbUnchanged (string sourcePath, string destinationPath)
		{
			string pdbSource = Path.ChangeExtension (sourcePath, "pdb");
			if (File.Exists (pdbSource)) {
				string pdbDestination = Path.ChangeExtension (destinationPath, "pdb");
				Files.CopyIfChanged (pdbSource, pdbDestination);
			}
		}
	}
}
