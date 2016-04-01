using System;
using System.Collections.Generic;


namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class ManagedApiQuery
	{
		public ManagedApiQuery ()
		{
			ParameterIndex = -1; // not a parameter query default
		}

		public string TypeName { get; set; }

		public string MemberName { get; set; }

		public IList<string> Arguments { get; set; }

		public int ParameterIndex { get; set; }
	}
}

