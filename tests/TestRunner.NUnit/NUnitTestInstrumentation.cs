using System;
using System.Collections.Generic;
using System.Linq;

using Android.OS;
using Android.Runtime;
using Android.Util;

using NUnit.Framework.Api;
using NUnit.Framework.Internal;
using NUnit.Framework.Internal.Filters;

namespace Xamarin.Android.UnitTests.NUnit
{
	public abstract class NUnitTestInstrumentation : TestInstrumentation <NUnitTestRunner>
	{
		protected IEnumerable<string> IncludedCategories { get; set; }
		protected IEnumerable<string> ExcludedCategories { get; set; }
		protected IEnumerable<string> ExcludedTestNames { get; set; }
		protected string TestsDirectory { get; set; }

		protected NUnitTestInstrumentation ()
		{
			CommonInit ();
		}

		protected NUnitTestInstrumentation (IntPtr handle, JniHandleOwnership transfer) : base (handle, transfer)
		{
			CommonInit ();
		}

		void CommonInit ()
		{
			LogTag = "NUnit";
		}

		protected override NUnitTestRunner CreateRunner (LogWriter logger, Bundle bundle)
		{
			return new NUnitTestRunner (Context, logger, bundle) {
				GCAfterEachFixture = true,
				TestsRootDirectory = TestsDirectory
			};
		}

		IEnumerable<string> GetFilterValuesFromExtras (string key)
		{
			Dictionary<string, string> extras = GetStringExtrasFromBundle ();
			if (extras.ContainsKey (key)) {
				string filterValue = extras [key];
				if (!string.IsNullOrEmpty (filterValue))
					return filterValue.Split (':');
			}
			return null;
		}

		protected override void ConfigureFilters (NUnitTestRunner runner)
		{
			base.ConfigureFilters(runner);

			if (runner == null)
				throw new ArgumentNullException (nameof (runner));
			
			ITestFilter filter = runner.Filter ?? TestFilter.Empty;

			Log.Info (LogTag, "Configuring test categories to include:");
			ChainCategoryFilter (IncludedCategories, false, ref filter);

			Log.Info (LogTag, "Configuring test categories to include from extras:");
			ChainCategoryFilter (GetFilterValuesFromExtras ("include"), false, ref filter);

			Log.Info (LogTag, "Configuring test categories to exclude:");
			ChainCategoryFilter (ExcludedCategories, true, ref filter);

			Log.Info(LogTag, "Configuring test categories to exclude from extras:");
			ChainCategoryFilter (GetFilterValuesFromExtras ("exclude"), true, ref filter);

			Log.Info (LogTag, "Configuring tests to exclude (by name):");
			ChainTestNameFilter (ExcludedTestNames?.ToArray (), ref filter);

			if (filter.IsEmpty)
				return;

			if (runner.Filter == null)
				runner.Filter = filter;
			else
				runner.Filter = new AndFilter (runner.Filter, filter);
		}

		void ChainCategoryFilter (IEnumerable <string> categories, bool negate, ref ITestFilter chain)
		{       
			bool gotCategories = false;
			if (categories != null) {
				var filter = new CategoryFilter ();
				foreach (string c in categories) {
					Log.Info (LogTag, $"  {c}");
					filter.AddCategory (c);
					gotCategories = true;
				}

				if (gotCategories)
					chain = new AndFilter (chain, negate ? new NotFilter (filter) : (ITestFilter)filter);
			}

			if (!gotCategories)
				Log.Info (LogTag, "  none");
		}

		void ChainTestNameFilter (string[] testNames, ref ITestFilter filter)
		{
			if (testNames == null || testNames.Length == 0) {
				Log.Info (LogTag, "  none");
				return;
			};

			foreach (string name in testNames) {
				if (String.IsNullOrEmpty (name))
					continue;
				Log.Info (LogTag, $"  {name}");
			}

			var excludeTestNamesFilter  = new SimpleNameFilter (testNames);
			filter = new AndFilter (filter, new NotFilter (excludeTestNamesFilter));
		}
	}
}
