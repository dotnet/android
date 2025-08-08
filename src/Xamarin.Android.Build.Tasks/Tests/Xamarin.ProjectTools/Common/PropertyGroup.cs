using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Represents a group of MSBuild properties with an optional condition.
	/// Property groups correspond to MSBuild &lt;PropertyGroup&gt; elements and
	/// allow organizing related properties together with shared conditions.
	/// </summary>
	/// <remarks>
	/// Property groups are commonly used to define configuration-specific settings
	/// (Debug vs Release) or platform-specific properties. The condition determines
	/// when the entire group of properties applies.
	/// </remarks>
	/// <seealso cref="Property"/>
	/// <seealso cref="XamarinProject.PropertyGroups"/>
	public class PropertyGroup
	{
		/// <summary>
		/// Initializes a new instance of the PropertyGroup class.
		/// </summary>
		/// <param name="condition">The MSBuild condition for when this property group applies.</param>
		/// <param name="properties">The collection of properties in this group.</param>
		public PropertyGroup (string condition, IList<Property> properties)
		{
			Condition = condition;
			Properties = properties;
		}
		
		/// <summary>
		/// Gets the MSBuild condition that determines when this property group applies.
		/// </summary>
		/// <remarks>
		/// If null or empty, the property group applies unconditionally. Otherwise,
		/// it applies only when the MSBuild condition evaluates to true.
		/// </remarks>
		public string Condition { get; private set; }
		
		/// <summary>
		/// Gets the collection of properties in this group.
		/// </summary>
		/// <seealso cref="Property"/>
		public IList<Property> Properties { get; private set; }
		
		/// <summary>
		/// Adds this property group to an MSBuild project root element.
		/// </summary>
		/// <param name="root">The project root element to add the property group to.</param>
		/// <remarks>
		/// This method creates the corresponding MSBuild XML elements for the property group
		/// and all its properties, applying conditions as appropriate.
		/// </remarks>
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
