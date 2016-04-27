using System;
using System.Collections.Generic;

namespace Xamarin.Android.Tools.Aidl
{
	public enum ParcelableHandling
	{
		Ignore,
		Error,
		Stub
	}
	
	public class ConverterOptions
	{
		public bool Verbose;
		public ParcelableHandling ParcelableHandling;
		public string OutputDirectory;
		public string OutputNS;
		public List<string> InputFiles = new List<string> ();
		public List<string> References = new List<string> ();
	}
}

