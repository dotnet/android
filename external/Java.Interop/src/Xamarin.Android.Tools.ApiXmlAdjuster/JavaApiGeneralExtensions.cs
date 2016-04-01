using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.Android.Tools.ApiXmlAdjuster
{
	public static class JavaApiGeneralExtensions
	{
		public static JavaApi GetApi (this JavaType type)
		{
			return type.Parent.Parent;
		}
		
		public static JavaApi GetApi (this JavaMember member)
		{
			return member.Parent.Parent.Parent;
		}
	}
	
}
