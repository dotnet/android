using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	
	public class PropertyGroup
	{
		public PropertyGroup (string condition, IList<Property> properties)
		{
			Condition = condition;
			Properties = properties;
		}
		
		public string Condition { get; private set; }
		public IList<Property> Properties { get; private set; }
		
		public void AddElement (ProjectRootElement root)
		{
			var ret = root.AddPropertyGroup ();
			ret.Condition = Condition;
			foreach (var p in Properties) {
				var value = p.Value ();
				if (string.IsNullOrWhiteSpace (value))
					continue;
				var pe = ret.AddProperty (p.Name, value);
				pe.Condition = p.Condition;
			}
		}
	}
}
