using System;
using System.Collections.Generic;
using System.Text;

namespace Xamarin.ProjectTools
{
	public interface IShortFormProject
	{
		/// <summary>
		/// If true, uses the default MSBuild wildcards for short-form projects.
		/// </summary>
		bool EnableDefaultItems { get; }
		string Sdk { get; set; }
		IList<PropertyGroup> PropertyGroups { get; }
		IList<BuildItem> References { get; }
		IList<Package> PackageReferences { get; }
		IList<BuildItem> OtherBuildItems { get; }
		IList<IList<BuildItem>> ItemGroupList { get; }
		IList<Import> Imports { get; }
		void SetProperty (string name, string value, string condition = null);
		bool RemoveProperty (string name);
	}
}
