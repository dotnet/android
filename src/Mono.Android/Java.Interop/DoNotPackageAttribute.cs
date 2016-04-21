using System;

namespace Java.Interop
{
	[Serializable]
	[AttributeUsage (AttributeTargets.Assembly,
			AllowMultiple=true,
			Inherited=false)]
	public class DoNotPackageAttribute : Attribute
	{
		public DoNotPackageAttribute (string jarFile)
		{
			JarFile = jarFile;
		}
		
		public string JarFile { get; set; }
	}
}

