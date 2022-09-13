using System;
using Android.Content;
using Google.Android.Material.TextField;

public class MaterialTextChanged
{
	// [Test]
	public static string TextChanged (Context context)
	{
		try {
			var view = new TextInputEditText (context);
			view.TextChanged += (s, e) => { };
			return $"[PASS] {nameof (MaterialTextChanged)}.{nameof (TextChanged)}";
		} catch (Exception ex) {
			return $"[FAIL] {nameof (MaterialTextChanged)}.{nameof (TextChanged)} FAILED: {ex}";
		}
	}
}
