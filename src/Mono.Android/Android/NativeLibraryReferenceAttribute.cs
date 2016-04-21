using System;

namespace Android {

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	sealed public class NativeLibraryReferenceAttribute : ReferenceFilesAttribute
	{
		public NativeLibraryReferenceAttribute (string filename)
		{
			LibraryFileName = filename;
		}

		public string   LibraryFileName {get; private set;}
	}
}
