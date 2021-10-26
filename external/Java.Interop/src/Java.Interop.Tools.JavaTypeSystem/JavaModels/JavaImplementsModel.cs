using System;
using System.Collections.Generic;

namespace Java.Interop.Tools.JavaTypeSystem.Models
{
	public class JavaImplementsModel
	{
		public string Name { get; }
		public string NameGeneric { get; }
		public string JniType { get; }

		public Dictionary<string, string> PropertyBag { get; } = new Dictionary<string, string> ();

		public JavaImplementsModel (string name, string nameGeneric, string jniType)
		{
			Name = name;
			NameGeneric = nameGeneric;
			JniType = jniType;
		}
	}
}
