using System;
using System.IO;
using NUnit.Framework;

namespace Xamarin.ProjectTools
{
	public partial class DownloadedCache
	{
		public DownloadedCache ()
			: this (Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "Xamarin.ProjectTools"))
		{
		}

		public DownloadedCache (string cacheDirectory)
			: this (cacheDirectory, message => TestContext.WriteLine (message), message => Assert.Inconclusive (message))
		{
		}
	}
}
