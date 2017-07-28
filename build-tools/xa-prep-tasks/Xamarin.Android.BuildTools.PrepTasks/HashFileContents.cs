using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class HashFileContents : Task
	{
		[Required]
		public      ITaskItem[]         Files                       { get; set; }


		public      string              HashAlgorithm               { get; set; } = "SHA1";

		public      int                 AbbreviatedHashLength       { get; set; } = 8;

		[Output]
		// Specifies %(Hashes.Target), %(Hashes.AbbreviatedHash). Hash is %(Hashes.Identity)).
		public      ITaskItem[]         Hashes                      { get; set;  }

		[Output]
		public      string              CompleteHash                { get; set;  }

		[Output]
		public      string              AbbreviatedCompleteHash     { get; set; }

		public override bool Execute ()
		{
			Log.LogMessage (MessageImportance.Low, $"Task {nameof (HashFileContents)}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (AbbreviatedHashLength)}: {AbbreviatedHashLength}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (HashAlgorithm)}: {HashAlgorithm}");
			Log.LogMessage (MessageImportance.Low, $"  {nameof (Files)}:");
			foreach (var e in Files) {
				Log.LogMessage (MessageImportance.Low, $"    {e.ItemSpec}");
			}

			ProcessFiles ();

			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (AbbreviatedCompleteHash)}: {AbbreviatedCompleteHash}");
			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (CompleteHash)}: {CompleteHash}");
			Log.LogMessage (MessageImportance.Low, $"  [Output] {nameof (Hashes)}:");
			foreach (var e in Hashes) {
				Log.LogMessage (MessageImportance.Low, $"    {e.GetMetadata ("Target")}: {e.ItemSpec}:");
			}
			return !Log.HasLoggedErrors;
		}

		void ProcessFiles ()
		{
			var     hashes  = new List<TaskItem> (Files.Length);
			byte[]  block   = new byte [4096];
			using (var complete = System.Security.Cryptography.HashAlgorithm.Create (HashAlgorithm)) {
				foreach (var file in Files) {
					var hash    = ProcessFile (complete, block, file.ItemSpec);
					var e       = new TaskItem (hash);
					e.SetMetadata ("Target", Path.GetFullPath (file.ItemSpec));
					e.SetMetadata ("AbbreviatedHash", hash.Substring (0, AbbreviatedHashLength));
					hashes.Add (e);
				}
				complete.TransformFinalBlock (block, 0, 0);
				CompleteHash            = FormatHash (complete.Hash);
				AbbreviatedCompleteHash = CompleteHash.Substring (0, AbbreviatedHashLength);
			}
			Hashes = hashes.ToArray ();
		}

		string ProcessFile (HashAlgorithm complete, byte[] block, string path)
		{
			using (var fileHash = System.Security.Cryptography.HashAlgorithm.Create (HashAlgorithm))
			using (var file = File.OpenRead (path)) {
				int read;
				while ((read = file.Read (block, 0, block.Length)) > 0) {
					complete.TransformBlock (block, 0, read, block, 0);
					fileHash.TransformBlock (block, 0, read, block, 0);
				}
				fileHash.TransformFinalBlock (block, 0, 0);
				return FormatHash (fileHash.Hash);
			}
		}

		string FormatHash (byte[] hash)
		{
			return string.Join ("", hash.Select (b => b.ToString ("x2")));
		}
	}
}

