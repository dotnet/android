using System;
using System.Collections.Generic;

using Microsoft.Build.Framework;

namespace Xamarin.Android.Tasks;

class RidAgnosticInputAssemblySet : InputAssemblySet
{
	Dictionary<string, ITaskItem> javaTypeAssemblies = new (AssemblyNameStringComparer);
	Dictionary<string, ITaskItem> userAssemblies = new (AssemblyNameStringComparer);

	public ICollection<ITaskItem> JavaTypeAssemblies => javaTypeAssemblies.Values;
	public ICollection<ITaskItem> UserAssemblies     => userAssemblies.Values;

	public override void AddJavaTypeAssembly (ITaskItem assemblyItem)
	{
		Add (assemblyItem.ItemSpec, assemblyItem, javaTypeAssemblies);
	}

	public override void AddUserAssembly (ITaskItem assemblyItem)
	{
		Add (GetUserAssemblyKey (assemblyItem), assemblyItem, userAssemblies);
	}

	public override bool IsUserAssembly (string name) => userAssemblies.ContainsKey (name);

	void Add (string key, ITaskItem assemblyItem, Dictionary<string, ITaskItem> dict)
	{
		if (dict.ContainsKey (key)) {
			return;
		}

		dict.Add (key, assemblyItem);
	}
}
