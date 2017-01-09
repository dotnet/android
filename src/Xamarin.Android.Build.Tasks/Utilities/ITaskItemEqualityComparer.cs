using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks
{
	public class ITaskItemEqualityComparer : IEqualityComparer<ITaskItem>
	{
		public bool Equals (ITaskItem x, ITaskItem y)
		{
			if (x == null || y == null)
				return false;
			return x.ItemSpec == y.ItemSpec;
		}

		public int GetHashCode (ITaskItem obj)
		{
			if (obj == null)
				return 0;
			return obj.ItemSpec.GetHashCode ();
		}
	}
}
