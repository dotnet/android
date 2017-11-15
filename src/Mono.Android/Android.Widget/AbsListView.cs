using System;
using System.Collections.Generic;

using Java.Interop;

using Android.Runtime;

namespace Android.Widget {

	partial class AbsListView {
		public override IListAdapter Adapter {
			get { throw new NotImplementedException ("This type " + GetType () + " should override Adapter property "); }
			set { throw new NotImplementedException ("This type " + GetType () + " should override Adapter property "); }
		}
	}
}
