using System;

namespace Java.Util.Concurrent.Atomic
{
	public partial class AtomicLong
	{
		[Obsolete ("This property was generated for getAndDecrement() method, while it does not make sense. It will be removed in the future version")]
		public long AndDecrement {
			get { return GetAndDecrement (); }
		}
		[Obsolete ("This property was generated for getAndIncrement() method, while it does not make sense. It will be removed in the future version")]
		public long AndIncrement {
			get { return GetAndIncrement (); }
		}
	}
}

