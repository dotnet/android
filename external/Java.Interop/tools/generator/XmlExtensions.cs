using System.Xml.Linq;
using Xamarin.Android.Tools;

namespace MonoDroid.Generation
{
	internal static class XmlExtensions
	{
		public static string Deprecated (this XElement elem)
		{
			var deprecated = elem.XGetAttribute ("deprecated");
			return deprecated != "not deprecated" ? deprecated : null;
		}

		public static string Visibility (this XElement elem) => elem.XGetAttribute ("visibility");

		public static GenericParameterDefinitionList GenericArguments (this XElement elem)
		{
			var tps = elem.Element ("typeParameters");
			return tps != null ? GenericParameterDefinitionList.FromXml (tps) : null;
		}
	}
}
