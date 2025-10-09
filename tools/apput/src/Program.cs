using System;

namespace ApplicationUtility;

class Program
{
	static int Main (string[] args)
	{
		Log.SetVerbose (true);
		try {
			return Run (args);
		} catch (Exception ex) {
			Log.ExceptionError ("Unhandled exception", ex);
			return 1;
		} finally {
			TempFileManager.Cleanup ();
		}
	}

	static int Run (string[] args)
	{
		IAspect? aspect = Detector.FindAspect (args[0]);
		if (aspect == null) {
			return 1;
		}
		Reporter.Report (aspect, plainTextRendering: false);
		return 0;
	}
}
