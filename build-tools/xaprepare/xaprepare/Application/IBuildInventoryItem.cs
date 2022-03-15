using System;

namespace Xamarin.Android.Prepare
{
	interface IBuildInventoryItem
	{
		string BuildToolName { get; }
		string BuildToolVersion { get; }
		void AddToInventory ();
	}
}
