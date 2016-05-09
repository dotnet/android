using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.Build.Construction;

namespace Xamarin.ProjectTools
{
	
	public class Property
	{
		public Property (string condition, string name, string value)
			: this (condition, name, () => value)
		{
		}

		public Property (string condition, string name, Func<string> value)
		{
			Condition = condition;
			Name = name;
			Value = value;
		}
		
		public string Name { get; set; }
		public Func<string> Value { get; set; }
		public string Condition { get; set; }
	}
}
