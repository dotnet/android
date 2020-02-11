using System;
using System.Collections.Generic;
using System.IO;

namespace Xamarin.Android.BuildTools.PrepTasks
{
	public class ProcessApkSizes : ProcessPlotInput
	{
		public string ReferenceFilename { get; set; }

		Dictionary<string, string> referenceResults = new Dictionary<string, string> ();

		public override bool Execute ()
		{
			if (!base.Execute ())
				return false;


			if (string.IsNullOrEmpty (ReferenceFilename) || !File.Exists (ReferenceFilename))
				return true;

			if (!ReadReference ()) {
				Log.LogError ($"unexpected reference results file {ReferenceFilename}");

				return false;
			}

			return CheckSizes ();
		}

		bool ReadReference ()
		{
			string line1 = null, line2 = null;
			using (var reader = new StreamReader (ReferenceFilename)) {
				try {
					line1 = reader.ReadLine ();
					line2 = reader.ReadLine ();
				} catch (Exception e) {
					Log.LogError ($"unable to read reference results from {ReferenceFilename}\n{e}");

					return false;
				}
			}

			if (string.IsNullOrEmpty (line1) || string.IsNullOrEmpty (line2))
				return false;

			var keys = line1.Split (new char [] { ',' });
			var values = line2.Split (new char [] { ',' });

			if (keys.Length != values.Length || keys.Length < 1)
				return false;

			for (int i = 0; i < keys.Length; i++)
				referenceResults [keys [i]] = values [i];

			return true;
		}

		string SizesMessage (int size, int refSize, int difference, string key)
		{
			return $"the reference {refSize} and actual size {size} for {key} differs too much (>{difference}kb).\nplease check if it is correct and update the reference apk sizes files if it is.\nit can be done by running `make CONFIGURATION=<Debug or Release> update-apk-sizes-reference` in XA root directory";
		}

		string SizesMessage2 (int size, int refSize, int difference, string key)
		{
			return $"the reference {refSize} is larger than actual size {size} for {key} and differs more than the set threshold (>{difference}kb).\nplease check if it is correct and update the reference apk sizes files if it is.\nit can be done by running `make CONFIGURATION=<Debug or Release> update-apk-sizes-reference` in XA root directory";
		}

		const int errorThreshold = 100;
		const int warningThreshold = 50;

		bool CheckSizes ()
		{
			foreach (var size in results) {
				var key = size.Key + LabelSuffix;
				var refSizeString = referenceResults [key];

				int refSize, resSize;

				if (!int.TryParse (refSizeString, out refSize)) {
					Log.LogError ($"unable to parse size of {key} in reference file {ReferenceFilename}");

					return false;
				}

				if (!int.TryParse (size.Value, out resSize)) {
					Log.LogError ($"unable to parse size of {key} in current results");

					return false;
				}

				if (resSize - refSize > errorThreshold * 1024) {
					Log.LogError (SizesMessage (resSize, refSize, errorThreshold, key));

					return false;
				}

				if (resSize - refSize > warningThreshold * 1024)
					Log.LogWarning (SizesMessage (resSize, refSize, warningThreshold, key));

				if (resSize < refSize && refSize - resSize > warningThreshold * 1024)
					Log.LogWarning (SizesMessage2 (resSize, refSize, warningThreshold, key));
			}

			return true;
		}
	}
}
