using Android.Content;

namespace Xamarin.Android.RuntimeTests {

	class MyIntent : Intent {

		public override System.Collections.Generic.IList<string> GetStringArrayListExtra (string name)
		{
			return name == "values"
				? new[]{"a", "b", "c"}
				: null;
		}
	}
}
