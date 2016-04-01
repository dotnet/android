using System;
using System.Collections.Generic;

namespace MonoDroid.Generation
{
	public interface IRequireGenericMarshal
	{
		string GetGenericJavaObjectTypeOverride ();
		string ToInteroperableJavaObject (string varname);
	}
}

