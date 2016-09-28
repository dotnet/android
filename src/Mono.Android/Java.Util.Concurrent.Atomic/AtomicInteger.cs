using System;

namespace Java.Util.Concurrent.Atomic
{
	public partial class AtomicInteger
	{
		[Obsolete ("This property was generated for getAndDecrement() method, while it does not make sense. It will be removed in the future version")]
		public int AndDecrement {
			get { return GetAndDecrement (); }
		}
		[Obsolete ("This property was generated for getAndIncrement() method, while it does not make sense. It will be removed in the future version")]
		public int AndIncrement {
			get { return GetAndIncrement (); }
		}
	}
}

