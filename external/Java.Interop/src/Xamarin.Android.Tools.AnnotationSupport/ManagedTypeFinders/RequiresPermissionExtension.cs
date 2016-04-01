using System;
using System.Collections.Generic;

namespace Xamarin.AndroidTools.AnnotationSupport
{
	public class RequiresPermissionExtension
	{
		public RequiresPermissionExtension ()
		{
			Values = new List<string> ();
		}

		public IList<string> Values { get; private set; }
	}
}

