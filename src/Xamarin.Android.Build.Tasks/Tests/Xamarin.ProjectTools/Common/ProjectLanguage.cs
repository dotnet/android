using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	public abstract class ProjectLanguage
	{
		public abstract string DefaultAssemblyInfo { get; }
		public abstract string DefaultExtension { get; }
		public abstract string DefaultProjectExtension { get; }
		public abstract string ProjectTypeGuid { get; }
	}

}
