using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public static class BuildActions
	{
		public const string None = "None";
		public const string ProjectReference = "ProjectReference";
		public const string Reference = "Reference";
		public const string Compile = "Compile";
		public const string EmbeddedResource = "EmbeddedResource";
		public const string Content = "Content";
		public const string Folder = "Folder";
	}
}
