using System;

namespace Xamarin.Android.Prepare
{
	class BundleItem
	{
		/// <summary>
		///   Optional path of the item in the destination archive
		/// </summary>
		public string ArchivePath                { get; }

		/// <summary>
		///   An optional functor to determine whether or not to include the item in the archive.
		/// </summary>
		public Func<Context, bool> ShouldInclude { get; }

		/// <summary>
		///   Required source path of the item.
		/// </summary>
		public string SourcePath                 { get; }

		public BundleItem (string sourcePath, string archivePath = null, Func<Context, bool> shouldInclude = null)
		{
			if (String.IsNullOrEmpty (sourcePath))
				throw new ArgumentNullException (nameof (sourcePath));

			ArchivePath = archivePath;
			ShouldInclude = shouldInclude;
			SourcePath = sourcePath;
		}
	}
}
