#nullable enable
using Microsoft.Android.Build.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Properties = Xamarin.Android.Tasks.Properties;

namespace Microsoft.Android.Tasks;

/// <summary>
/// Minimal <see cref="ITaskItem"/> helpers for <see cref="CompressAssemblies"/>, duplicated
/// here to keep this net11.0 assembly self-contained (the full versions live in
/// <c>ITaskItemExtensions</c> in Xamarin.Android.Build.Tasks).
/// </summary>
static class TaskItemExtensions
{
	public static bool TryGetRequiredMetadata (this ITaskItem item, string itemName, string name, TaskLoggingHelper log, out string value)
	{
		value = item.GetMetadata (name);

		if (string.IsNullOrWhiteSpace (value)) {
			log.LogCodedError ("XA4234", Properties.Resources.XA4234, itemName, item.ItemSpec, name);
			return false;
		}

		return true;
	}
}
