using System;
using System.IO;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	/// <summary>
	/// Reads "XamarinAndroidVersion=10.2" from $(_AndroidBuildPropertiesCache) / build.props
	/// </summary>
	public class ReadXamarinAndroidVersion : AndroidTask
	{
		public override string TaskPrefix => "RXAV";

		[Required]
		public string BuildPropertiesCache { get; set; }

		[Output]
		public string XamarinAndroidVersion { get; set; }

		public override bool RunTask ()
		{
			if (File.Exists (BuildPropertiesCache)) {
				using (var reader = File.OpenText (BuildPropertiesCache)) {
					while (!reader.EndOfStream) {
						string line = reader.ReadLine ();
						int index = line.IndexOf ('=');
						if (index != -1) {
							string key = line.Substring (0, index);
							if (string.Equals ("XamarinAndroidVersion", key, StringComparison.OrdinalIgnoreCase)) {
								XamarinAndroidVersion = line.Substring (index + 1, line.Length - index - 1);
								break;
							}
						}
					}
				}
			}
			return !Log.HasLoggedErrors;
		}
	}
}
