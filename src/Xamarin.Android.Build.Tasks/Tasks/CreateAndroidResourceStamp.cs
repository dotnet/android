using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.Android.Tasks
{
	public class CreateAndroidResourceStamp : Task
	{
		[Required]
		public string AndroidResgenFile { get; set; }
		[Required]
		public string [] AndroidResourceDest { get; set; }
		[Required]
		public string MonoAndroidResDirIntermediate { get; set; }
		[Required]
		public string AndroidResgenFlagFile { get; set; }

		public override bool Execute ()
		{
			if ((AndroidResourceDest == null || AndroidResourceDest.Length == 0) && File.Exists (AndroidResgenFile))
				File.Delete (AndroidResgenFile);
			Directory.CreateDirectory (MonoAndroidResDirIntermediate);
			// touch resgen file.
			if (!File.Exists (AndroidResgenFile) && Directory.Exists (Path.GetDirectoryName (AndroidResgenFile))) {
				File.AppendText (AndroidResgenFile).Close ();
				MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (AndroidResgenFile, DateTime.UtcNow, Log);
			}
			// touch resgen flag file
			File.AppendText (AndroidResgenFlagFile).Close ();
			MonoAndroidHelper.SetLastAccessAndWriteTimeUtc (AndroidResgenFlagFile, DateTime.UtcNow, Log);

			return true;
		}
	}
}

