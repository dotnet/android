using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	/// <summary>
	/// Represents a NuGet package reference in a test project.
	/// Used to generate PackageReference items and packages.config entries
	/// for testing NuGet package integration scenarios.
	/// </summary>
	/// <remarks>
	/// This class corresponds to NuGet package references in MSBuild projects.
	/// It can represent both modern PackageReference style (SDK-style projects)
	/// and legacy packages.config style references.
	/// Example: &lt;PackageReference Include="Xamarin.AndroidX.Core" Version="1.8.0" /&gt;
	/// </remarks>
	/// <seealso cref="XamarinProject.PackageReferences"/>
	/// <seealso cref="BuildItem.Reference"/>
	public class Package
	{
		/// <summary>
		/// Initializes a new instance of the Package class with default settings.
		/// </summary>
		public Package ()
		{
			AutoAddReferences = true;
			References = new List<BuildItem.Reference> ();
		}

		/// <summary>
		/// Initializes a new instance of the Package class by copying another package.
		/// </summary>
		/// <param name="other">The package to copy settings from.</param>
		/// <param name="audoAddReferences">Whether to automatically add assembly references from this package.</param>
		public Package (Package other, bool audoAddReferences)
		{
			Id = other.Id;
			Version = other.Version;
			TargetFramework = other.TargetFramework;
			AutoAddReferences = audoAddReferences;
			References = new List<BuildItem.Reference> (other.References);
		}

		/// <summary>
		/// Gets or sets a value indicating whether assembly references from this package should be automatically added to the project.
		/// </summary>
		public bool AutoAddReferences { get; set; }
		
		/// <summary>
		/// Gets or sets the package identifier (e.g., "Xamarin.AndroidX.Core").
		/// </summary>
		public string Id { get; set; }
		
		/// <summary>
		/// Gets or sets the package version (e.g., "1.8.0").
		/// </summary>
		public string Version { get; set; }
		
		/// <summary>
		/// Gets or sets the target framework for this package reference (used in packages.config format).
		/// </summary>
		/// <remarks>
		/// This is primarily used for legacy packages.config format. In modern PackageReference
		/// format, the target framework is typically inferred from the project's TargetFramework.
		/// </remarks>
		public string TargetFramework { get; set; }
		
		/// <summary>
		/// Gets the collection of assembly references that this package provides.
		/// </summary>
		/// <seealso cref="BuildItem.Reference"/>
		public IList<BuildItem.Reference> References { get; private set; }
	}

}
