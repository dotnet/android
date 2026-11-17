using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiGeneralExtensions
	{
		public static JavaApi GetApi (this JavaType type)
		{
			return type.Parent?.Parent ?? throw new InvalidOperationException ("`JavaApi` via JavaType.Parent.Parent not set!");
		}
		
		public static JavaApi GetApi (this JavaMember member)
		{
			return member.Parent?.Parent?.Parent ?? throw new InvalidOperationException ("`JavaApi` via JavaMethod.Parent.Parent.Parent not set!");;
		}
	}
	
}
