using System;
using System.Collections.Generic;
using Android.Runtime;

namespace Android.Widget {
	public partial class AutoCompleteTextView
	{
		List<EventHandler> selection_cleared;
		
		void OnSelectionCleared (object o, Android.Widget.AdapterView.NothingSelectedEventArgs args)
		{
			foreach (var h in selection_cleared)
				h (o, EventArgs.Empty);
		}
		
		[Obsolete ("Use NothingSelected event instead")]
		public event EventHandler ItemSelectionCleared {
			add {
				if (selection_cleared == null) {
					selection_cleared = new List<EventHandler> ();
					NothingSelected += OnSelectionCleared;
				}
				selection_cleared.Add (value);
			}
			remove {
				selection_cleared.Remove (value);
			}
		}
	}
}
