using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

using Android.Runtime;

using Java.Interop;
using Java.Interop.Expressions;

namespace Android.Graphics
{
	[JniValueMarshaler (typeof (ColorValueMarshaler))]
	public struct Color
	{
		private int color;

		#region Constuctors
		public Color (int argb)
		{
			color = argb;
		}

		private Color (uint argb)
		{
			color = (int)argb;
		}

		public Color (byte r, byte g, byte b)
		{
			color = FromRgb ((int)r, (int)g, (int)b);
		}

		public Color (int r, int g, int b)
		{
			color = FromRgb (r, g, b);
		}

		public Color (byte r, byte g, byte b, byte a)
		{
			color = FromArgb ((int)r, (int)g, (int)b, (int)a);
		}

		public Color (int r, int g, int b, int a)
		{
			color = FromArgb (r, g, b, a);
		}
		#endregion

		#region Public Properties
		public byte A { get { return (byte)(color >> 24); } set { color = FromArgb (R, G, B, value); } }
		public byte B { get { return (byte)color; } set { color = FromArgb (R, G, value, A); } }
		public byte G { get { return (byte)(color >> 8); } set { color = FromArgb (R, value, B, A); } }
		public byte R { get { return (byte)(color >> 16); } set { color = FromArgb (value, G, B, A); } }
		#endregion

		#region Public Methods
		public int ToArgb ()
		{
			return color;
		}

		public override string ToString ()
		{
			return FormattableString.Invariant ($"Color [A={A}, R={R}, G={G}, B={B}]");
		}

		public override bool Equals (object obj)
		{
			if (!(obj is Color))
				return false;
			Color c = (Color)obj;
			return this == c;
		}

		public float GetBrightness ()
		{
			byte minval = Math.Min (R, Math.Min (G, B));
			byte maxval = Math.Max (R, Math.Max (G, B));

			return (float)(maxval + minval) / 510;
		}

		public float GetSaturation ()
		{
			byte minval = (byte)Math.Min (R, Math.Min (G, B));
			byte maxval = (byte)Math.Max (R, Math.Max (G, B));

			if (maxval == minval)
				return 0.0f;

			int sum = maxval + minval;
			if (sum > 255)
				sum = 510 - sum;

			return (float)(maxval - minval) / sum;
		}

		public float GetHue ()
		{
			int r = R;
			int g = G;
			int b = B;
			byte minval = (byte)Math.Min (r, Math.Min (g, b));
			byte maxval = (byte)Math.Max (r, Math.Max (g, b));

			if (maxval == minval)
				return 0.0f;

			float diff = (float)(maxval - minval);
			float rnorm = (maxval - r) / diff;
			float gnorm = (maxval - g) / diff;
			float bnorm = (maxval - b) / diff;

			float hue = 0.0f;
			if (r == maxval)
				hue = 60.0f * (6.0f + bnorm - gnorm);
			if (g == maxval)
				hue = 60.0f * (2.0f + rnorm - bnorm);
			if (b == maxval)
				hue = 60.0f * (4.0f + gnorm - rnorm);
			if (hue > 360.0f)
				hue = hue - 360.0f;

			return hue;
		}
		#endregion

		#region Public Static Methods
		public static int GetAlphaComponent (int color)
		{
			return (byte)(color >> 24);
		}

		public static int GetBlueComponent (int color)
		{
			return (byte)color;
		}

		public static int GetGreenComponent (int color)
		{
			return (byte)(color >> 8);
		}

		public static int GetRedComponent (int color)
		{
			return (byte)(color >> 16);
		}

		public static Color Argb (int alpha, int red, int green, int blue)
		{
			return new Color ((int)((uint)alpha << 24) | (red << 16) | (green << 8) | blue);
		}

		public static Color Rgb (int red, int green, int blue)
		{
			int alpha = 255;
			return new Color ((int)((uint)alpha << 24) | (red << 16) | (green << 8) | blue);
		}
		#endregion

		#region Operators
		public static bool operator == (Color left, Color right)
		{
			return left.color == right.color;
		}

		public static bool operator != (Color left, Color right)
		{
			return !(left == right);
		}

		public static implicit operator int (Color c)
		{
			return c.color;
		}

		public override int GetHashCode ()
		{
			return color;
		}
		#endregion

		#region Private Methods
		private static int FromRgb (int red, int green, int blue)
		{
			int alpha = 255;

			CheckARGBValues (alpha, red, green, blue);
			return (int)((uint)alpha << 24) + (red << 16) + (green << 8) + blue;
		}

		private static int FromArgb (int red, int green, int blue, int alpha)
		{
			CheckARGBValues (alpha, red, green, blue);
			return (int)((uint)alpha << 24) + (red << 16) + (green << 8) + blue;
		}

		private static void CheckARGBValues (int alpha, int red, int green, int blue)
		{
			if ((alpha > 255) || (alpha < 0))
				throw CreateColorArgumentException (alpha, "alpha");
			CheckRGBValues (red, green, blue);
		}

		private static void CheckRGBValues (int red, int green, int blue)
		{
			if ((red > 255) || (red < 0))
				throw CreateColorArgumentException (red, "red");
			if ((green > 255) || (green < 0))
				throw CreateColorArgumentException (green, "green");
			if ((blue > 255) || (blue < 0))
				throw CreateColorArgumentException (blue, "blue");
		}

		private static ArgumentException CreateColorArgumentException (int value, string color)
		{
			return new ArgumentException (FormattableString.Invariant (
				$"'{value}' is not a valid  value for '{color}'. '{color}' should be greater or equal to 0 and less than or equal to 255."));
		}

		public static Color ParseColor (string colorString)
		{
			return new Color (ColorObject.ParseColor (colorString));
		}

		public static void ColorToHSV (Android.Graphics.Color color, float[] hsv)
		{
			ColorObject.ColorToHSV (color, hsv);
		}

		public static Color HSVToColor (float[] hsv)
		{
			return new Color (ColorObject.HSVToColor (hsv));
		}

		public static Color HSVToColor (int alpha, float[] hsv)
		{
			return new Color (ColorObject.HSVToColor (alpha, hsv));
		}

		public static void RGBToHSV (int red, int green, int blue, float[] hsv)
		{
			ColorObject.RGBToHSV (red, green, blue, hsv);
		}
		#endregion

		#region Known Colors
		public static Color Transparent { get { return new Color (0x000000); } }
		public static Color AliceBlue { get { return new Color (0xFFF0F8FF); } }
		public static Color AntiqueWhite { get { return new Color (0xFFFAEBD7); } }
		public static Color Aqua { get { return new Color (0xFF00FFFF); } }
		public static Color Aquamarine { get { return new Color (0xFF7FFFD4); } }
		public static Color Azure { get { return new Color (0xFFF0FFFF); } }
		public static Color Beige { get { return new Color (0xFFF5F5DC); } }
		public static Color Bisque { get { return new Color (0xFFFFE4C4); } }
		public static Color Black { get { return new Color (0xFF000000); } }
		public static Color BlanchedAlmond { get { return new Color (0xFFFFEBCD); } }
		public static Color Blue { get { return new Color (0xFF0000FF); } }
		public static Color BlueViolet { get { return new Color (0xFF8A2BE2); } }
		public static Color Brown { get { return new Color (0xFFA52A2A); } }
		public static Color BurlyWood { get { return new Color (0xFFDEB887); } }
		public static Color CadetBlue { get { return new Color (0xFF5F9EA0); } }
		public static Color Chartreuse { get { return new Color (0xFF7FFF00); } }
		public static Color Chocolate { get { return new Color (0xFFD2691E); } }
		public static Color Coral { get { return new Color (0xFFFF7F50); } }
		public static Color CornflowerBlue { get { return new Color (0xFF6495ED); } }
		public static Color Cornsilk { get { return new Color (0xFFFFF8DC); } }
		public static Color Crimson { get { return new Color (0xFFDC143C); } }
		public static Color Cyan { get { return new Color (0xFF00FFFF); } }
		public static Color DarkBlue { get { return new Color (0xFF00008B); } }
		public static Color DarkCyan { get { return new Color (0xFF008B8B); } }
		public static Color DarkGoldenrod { get { return new Color (0xFFB8860B); } }
		public static Color DarkGray { get { return new Color (0xFF444444); } }
		public static Color DarkGreen { get { return new Color (0xFF006400); } }
		public static Color DarkKhaki { get { return new Color (0xFFBDB76B); } }
		public static Color DarkMagenta { get { return new Color (0xFF8B008B); } }
		public static Color DarkOliveGreen { get { return new Color (0xFF556B2F); } }
		public static Color DarkOrange { get { return new Color (0xFFFF8C00); } }
		public static Color DarkOrchid { get { return new Color (0xFF9932CC); } }
		public static Color DarkRed { get { return new Color (0xFF8B0000); } }
		public static Color DarkSalmon { get { return new Color (0xFFE9967A); } }
		public static Color DarkSeaGreen { get { return new Color (0xFF8FBC8B); } }
		public static Color DarkSlateBlue { get { return new Color (0xFF483D8B); } }
		public static Color DarkSlateGray { get { return new Color (0xFF2F4F4F); } }
		public static Color DarkTurquoise { get { return new Color (0xFF00CED1); } }
		public static Color DarkViolet { get { return new Color (0xFF9400D3); } }
		public static Color DeepPink { get { return new Color (0xFFFF1493); } }
		public static Color DeepSkyBlue { get { return new Color (0xFF00BFFF); } }
		public static Color DimGray { get { return new Color (0xFF696969); } }
		public static Color DodgerBlue { get { return new Color (0xFF1E90FF); } }
		public static Color Firebrick { get { return new Color (0xFFB22222); } }
		public static Color FloralWhite { get { return new Color (0xFFFFFAF0); } }
		public static Color ForestGreen { get { return new Color (0xFF228B22); } }
		public static Color Fuchsia { get { return new Color (0xFFFF00FF); } }
		public static Color Gainsboro { get { return new Color (0xFFDCDCDC); } }
		public static Color GhostWhite { get { return new Color (0xFFF8F8FF); } }
		public static Color Gold { get { return new Color (0xFFFFD700); } }
		public static Color Goldenrod { get { return new Color (0xFFDAA520); } }
		public static Color Gray { get { return new Color (0xFF888888); } }
		public static Color Green { get { return new Color (0xFF00FF00); } }
		public static Color GreenYellow { get { return new Color (0xFFADFF2F); } }
		public static Color Honeydew { get { return new Color (0xFFF0FFF0); } }
		public static Color HotPink { get { return new Color (0xFFFF69B4); } }
		public static Color IndianRed { get { return new Color (0xFFCD5C5C); } }
		public static Color Indigo { get { return new Color (0xFF4B0082); } }
		public static Color Ivory { get { return new Color (0xFFFFFFF0); } }
		public static Color Khaki { get { return new Color (0xFFF0E68C); } }
		public static Color Lavender { get { return new Color (0xFFE6E6FA); } }
		public static Color LavenderBlush { get { return new Color (0xFFFFF0F5); } }
		public static Color LawnGreen { get { return new Color (0xFF7CFC00); } }
		public static Color LemonChiffon { get { return new Color (0xFFFFFACD); } }
		public static Color LightBlue { get { return new Color (0xFFADD8E6); } }
		public static Color LightCoral { get { return new Color (0xFFF08080); } }
		public static Color LightCyan { get { return new Color (0xFFE0FFFF); } }
		public static Color LightGoldenrodYellow { get { return new Color (0xFFFAFAD2); } }
		public static Color LightGreen { get { return new Color (0xFF90EE90); } }
		public static Color LightGray { get { return new Color (0xFFCCCCCC); } }
		public static Color LightPink { get { return new Color (0xFFFFB6C1); } }
		public static Color LightSalmon { get { return new Color (0xFFFFA07A); } }
		public static Color LightSeaGreen { get { return new Color (0xFF20B2AA); } }
		public static Color LightSkyBlue { get { return new Color (0xFF87CEFA); } }
		public static Color LightSlateGray { get { return new Color (0xFF778899); } }
		public static Color LightSteelBlue { get { return new Color (0xFFB0C4DE); } }
		public static Color LightYellow { get { return new Color (0xFFFFFFE0); } }
		public static Color Lime { get { return new Color (0xFF00FF00); } }
		public static Color LimeGreen { get { return new Color (0xFF32CD32); } }
		public static Color Linen { get { return new Color (0xFFFAF0E6); } }
		public static Color Magenta { get { return new Color (0xFFFF00FF); } }
		public static Color Maroon { get { return new Color (0xFF800000); } }
		public static Color MediumAquamarine { get { return new Color (0xFF66CDAA); } }
		public static Color MediumBlue { get { return new Color (0xFF0000CD); } }
		public static Color MediumOrchid { get { return new Color (0xFFBA55D3); } }
		public static Color MediumPurple { get { return new Color (0xFF9370DB); } }
		public static Color MediumSeaGreen { get { return new Color (0xFF3CB371); } }
		public static Color MediumSlateBlue { get { return new Color (0xFF7B68EE); } }
		public static Color MediumSpringGreen { get { return new Color (0xFF00FA9A); } }
		public static Color MediumTurquoise { get { return new Color (0xFF48D1CC); } }
		public static Color MediumVioletRed { get { return new Color (0xFFC71585); } }
		public static Color MidnightBlue { get { return new Color (0xFF191970); } }
		public static Color MintCream { get { return new Color (0xFFF5FFFA); } }
		public static Color MistyRose { get { return new Color (0xFFFFE4E1); } }
		public static Color Moccasin { get { return new Color (0xFFFFE4B5); } }
		public static Color NavajoWhite { get { return new Color (0xFFFFDEAD); } }
		public static Color Navy { get { return new Color (0xFF000080); } }
		public static Color OldLace { get { return new Color (0xFFFDF5E6); } }
		public static Color Olive { get { return new Color (0xFF808000); } }
		public static Color OliveDrab { get { return new Color (0xFF6B8E23); } }
		public static Color Orange { get { return new Color (0xFFFFA500); } }
		public static Color OrangeRed { get { return new Color (0xFFFF4500); } }
		public static Color Orchid { get { return new Color (0xFFDA70D6); } }
		public static Color PaleGoldenrod { get { return new Color (0xFFEEE8AA); } }
		public static Color PaleGreen { get { return new Color (0xFF98FB98); } }
		public static Color PaleTurquoise { get { return new Color (0xFFAFEEEE); } }
		public static Color PaleVioletRed { get { return new Color (0xFFDB7093); } }
		public static Color PapayaWhip { get { return new Color (0xFFFFEFD5); } }
		public static Color PeachPuff { get { return new Color (0xFFFFDAB9); } }
		public static Color Peru { get { return new Color (0xFFCD853F); } }
		public static Color Pink { get { return new Color (0xFFFFC0CB); } }
		public static Color Plum { get { return new Color (0xFFDDA0DD); } }
		public static Color PowderBlue { get { return new Color (0xFFB0E0E6); } }
		public static Color Purple { get { return new Color (0xFF800080); } }
		public static Color Red { get { return new Color (0xFFFF0000); } }
		public static Color RosyBrown { get { return new Color (0xFFBC8F8F); } }
		public static Color RoyalBlue { get { return new Color (0xFF4169E1); } }
		public static Color SaddleBrown { get { return new Color (0xFF8B4513); } }
		public static Color Salmon { get { return new Color (0xFFFA8072); } }
		public static Color SandyBrown { get { return new Color (0xFFF4A460); } }
		public static Color SeaGreen { get { return new Color (0xFF2E8B57); } }
		public static Color SeaShell { get { return new Color (0xFFFFF5EE); } }
		public static Color Sienna { get { return new Color (0xFFA0522D); } }
		public static Color Silver { get { return new Color (0xFFC0C0C0); } }
		public static Color SkyBlue { get { return new Color (0xFF87CEEB); } }
		public static Color SlateBlue { get { return new Color (0xFF6A5ACD); } }
		public static Color SlateGray { get { return new Color (0xFF708090); } }
		public static Color Snow { get { return new Color (0xFFFFFAFA); } }
		public static Color SpringGreen { get { return new Color (0xFF00FF7F); } }
		public static Color SteelBlue { get { return new Color (0xFF4682B4); } }
		public static Color Tan { get { return new Color (0xFFD2B48C); } }
		public static Color Teal { get { return new Color (0xFF008080); } }
		public static Color Thistle { get { return new Color (0xFFD8BFD8); } }
		public static Color Tomato { get { return new Color (0xFFFF6347); } }
		public static Color Turquoise { get { return new Color (0xFF40E0D0); } }
		public static Color Violet { get { return new Color (0xFFEE82EE); } }
		public static Color Wheat { get { return new Color (0xFFF5DEB3); } }
		public static Color White { get { return new Color (0xFFFFFFFF); } }
		public static Color WhiteSmoke { get { return new Color (0xFFF5F5F5); } }
		public static Color Yellow { get { return new Color (0xFFFFFF00); } }
		public static Color YellowGreen { get { return new Color (0xFF9ACD32); } }
		#endregion
	}

	public class ColorValueMarshaler : JniValueMarshaler<Color>
	{
		const DynamicallyAccessedMemberTypes ConstructorsAndInterfaces = DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.NonPublicConstructors | DynamicallyAccessedMemberTypes.Interfaces;
		const string ExpressionRequiresUnreferencedCode = "System.Linq.Expression usage may trim away required code.";

		public override Type MarshalType {
			get { return typeof (int); }
		}

		public override Color CreateGenericValue (
				ref JniObjectReference reference,
				JniObjectReferenceOptions options,
				[DynamicallyAccessedMembers (ConstructorsAndInterfaces)]
				Type targetType)
		{
			throw new NotImplementedException ();
		}

		public override JniValueMarshalerState CreateGenericObjectReferenceArgumentState ([MaybeNull]Color value, ParameterAttributes synchronize)
		{
			throw new NotImplementedException ();
		}

		public override void DestroyGenericArgumentState (Color value, ref JniValueMarshalerState state, ParameterAttributes synchronize)
		{
			throw new NotImplementedException ();
		}

		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize, Type targetType)
		{
			var c = typeof (Color).GetConstructor (new[]{typeof (int)})!;
			var v = Expression.Variable (typeof (Color), sourceValue.Name + "_val");
			context.LocalVariables.Add (v);
			context.CreationStatements.Add (Expression.Assign (v, Expression.New (c, sourceValue)));

			return v;
		}

		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateParameterFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize)
		{
			var r = Expression.Variable (MarshalType, sourceValue.Name + "_p");
			context.LocalVariables.Add (r);
			context.CreationStatements.Add (Expression.Assign (r, Expression.Field (sourceValue, "color")));

			return r;
		}

		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			return CreateParameterFromManagedExpression (context, sourceValue, 0);
		}
	}
}
