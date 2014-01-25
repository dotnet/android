using System;

namespace Java.Interop {

	[AttributeUsage (AttributeTargets.Method, AllowMultiple=false)]
	public sealed class ExportAttribute : Attribute {

		public ExportAttribute ()
		{
		}

		public ExportAttribute (string name)
		{
			Name = name;
		}

		public  string  Name        {get; private set;}
		public  string  Signature   {get; set;}
	}
}