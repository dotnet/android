using System;

using Android.Content;

namespace Android.App {

	public partial class SearchManager {

		public static SearchManager FromContext (Context context)
		{
			return context.GetSystemService (Context.SearchService) as SearchManager;
		}
	}
}


