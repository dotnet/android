using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

namespace Xamarin.Android.Build.Tests
{
	/// <summary>
	/// Supplies <see cref="BaseTest.ValidRuntimeConfigurations"/> to a <c>[CombinatorialData]</c> test parameter.
	/// </summary>
	[AttributeUsage (AttributeTargets.Parameter)]
	public sealed class ValidRuntimeConfigurationsAttribute : Attribute, ICombinatorialValuesProvider
	{
		public object [] GetValues (ParameterInfo parameter) =>
			BaseTest.ValidRuntimeConfigurations.Cast<object> ().ToArray ();
	}
}
