using System;

namespace Android {

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	[Obsolete ("This attribute is not longer supported.", error: true)]
	public class IncludeAndroidResourcesFromAttribute : ReferenceFilesAttribute
	{
		public IncludeAndroidResourcesFromAttribute (string path)
		{
			ResourceDirectory = path;
		}

		public string   ResourceDirectory {get; private set;}
	}
}
