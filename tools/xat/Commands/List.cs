using System;
using System.Threading.Tasks;

using Xamarin.Android.Prepare;

namespace Xamarin.Android.Tests
{
	class List : XatCommand
	{
		ListItem whatToList;

		public List (ListItem whatToList)
		{
			this.whatToList = whatToList;
		}

#pragma warning disable 1998
		public override async Task<bool> Invoke ()
		{
			if (whatToList.HasFlag (ListItem.Suites)) {
				ListSuites ();
			}

			if (whatToList.HasFlag (ListItem.Groups)) {
				ListGroups ();
			}

			return true;
		}
#pragma warning restore 1998

		void ListSuites ()
		{
			foreach (var kvp in Context.Instance.Tests.AllSuitesByName) {
				string testName = kvp.Key;
				XATest test = kvp.Value;

				Log.Message ($" {Context.Characters.Bullet} ");
				Log.Message (test.ID, ConsoleColor.Cyan);
				Log.Message ($" ({test.KindName}", ConsoleColor.White);
				Log.MessageLine ($": '{test.Name}')");
			}
		}

		void ListGroups ()
		{
			Log.WarningLine ($"  not implemented yet (would list {whatToList})");
		}
	}
}
