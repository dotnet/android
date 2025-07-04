namespace ApplicationUtility;

class Program
{
    static void Main (string[] args)
    {
	    Log.SetVerbose (true);
	    IAspect? aspect = Detector.FindAspect (args[0]);
    }
}
