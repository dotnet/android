using System;

namespace Android {

	[AttributeUsage (AttributeTargets.Assembly, AllowMultiple = true)]
	[Obsolete ("This attribute is deprecated and will be removed in a future release.")]
	public class IncludeAndroidResourcesFromAttribute : ReferenceFilesAttribute
	{
		public IncludeAndroidResourcesFromAttribute (string path)
		{
			ResourceDirectory = path;
		}

		public string   ResourceDirectory {get; private set;}
	}
}
