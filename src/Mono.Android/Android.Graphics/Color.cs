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

		/// <summary>
		/// Gets or sets the alpha (opacity) component of the color, where 0 is fully transparent and 255 is fully opaque.
		/// </summary>
		/// <value>A <see cref="byte"/> representing the alpha component, in the range 0–255.</value>
		public byte A { get { return (byte)(color >> 24); } set { color = FromArgb (R, G, B, value); } }

		/// <summary>
		/// Gets or sets the blue component of the color.
		/// </summary>
		/// <value>A <see cref="byte"/> representing the blue component, in the range 0–255.</value>
		public byte B { get { return (byte)color; } set { color = FromArgb (R, G, value, A); } }

		/// <summary>
		/// Gets or sets the green component of the color.
		/// </summary>
		/// <value>A <see cref="byte"/> representing the green component, in the range 0–255.</value>
		public byte G { get { return (byte)(color >> 8); } set { color = FromArgb (R, value, B, A); } }

		/// <summary>
		/// Gets or sets the red component of the color.
		/// </summary>
		/// <value>A <see cref="byte"/> representing the red component, in the range 0–255.</value>
		public byte R { get { return (byte)(color >> 16); } set { color = FromArgb (value, G, B, A); } }

		public int ToArgb ()
		{
			return color;
		}

		public override string ToString ()
		{
			return FormattableString.Invariant ($"Color [A={A}, R={R}, G={G}, B={B}]");
		}

		public override bool Equals (object? obj)
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

		/// <summary>
		/// Extracts the alpha (opacity) component from a packed ARGB color integer, where 0 is fully transparent and 255 is fully opaque.
		/// </summary>
		/// <param name="color">A packed ARGB color integer, such as the value returned by <see cref="ToArgb"/>.</param>
		/// <returns>The alpha component of <paramref name="color"/>, in the range 0–255.</returns>
		public static int GetAlphaComponent (int color)
		{
			return (byte)(color >> 24);
		}

		/// <summary>
		/// Extracts the blue component from a packed ARGB color integer.
		/// </summary>
		/// <param name="color">A packed ARGB color integer, such as the value returned by <see cref="ToArgb"/>.</param>
		/// <returns>The blue component of <paramref name="color"/>, in the range 0–255.</returns>
		public static int GetBlueComponent (int color)
		{
			return (byte)color;
		}

		/// <summary>
		/// Extracts the green component from a packed ARGB color integer.
		/// </summary>
		/// <param name="color">A packed ARGB color integer, such as the value returned by <see cref="ToArgb"/>.</param>
		/// <returns>The green component of <paramref name="color"/>, in the range 0–255.</returns>
		public static int GetGreenComponent (int color)
		{
			return (byte)(color >> 8);
		}

		/// <summary>
		/// Extracts the red component from a packed ARGB color integer.
		/// </summary>
		/// <param name="color">A packed ARGB color integer, such as the value returned by <see cref="ToArgb"/>.</param>
		/// <returns>The red component of <paramref name="color"/>, in the range 0–255.</returns>
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

		/// <summary>Gets a fully transparent <see cref="Color" /> (ARGB #00000000).</summary>
		public static Color Transparent { get { return new Color (0x000000); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF0F8FF (Alice blue).</summary>
		public static Color AliceBlue { get { return new Color (0xFFF0F8FF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFAEBD7 (antique white).</summary>
		public static Color AntiqueWhite { get { return new Color (0xFFFAEBD7); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF00FFFF (aqua).</summary>
		public static Color Aqua { get { return new Color (0xFF00FFFF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF7FFFD4 (aquamarine).</summary>
		public static Color Aquamarine { get { return new Color (0xFF7FFFD4); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF0FFFF (azure).</summary>
		public static Color Azure { get { return new Color (0xFFF0FFFF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF5F5DC (beige).</summary>
		public static Color Beige { get { return new Color (0xFFF5F5DC); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFE4C4 (bisque).</summary>
		public static Color Bisque { get { return new Color (0xFFFFE4C4); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF000000 (black).</summary>
		public static Color Black { get { return new Color (0xFF000000); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFEBCD (blanched almond).</summary>
		public static Color BlanchedAlmond { get { return new Color (0xFFFFEBCD); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF0000FF (blue).</summary>
		public static Color Blue { get { return new Color (0xFF0000FF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF8A2BE2 (blue-violet).</summary>
		public static Color BlueViolet { get { return new Color (0xFF8A2BE2); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFA52A2A (brown).</summary>
		public static Color Brown { get { return new Color (0xFFA52A2A); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFDEB887 (burly wood).</summary>
		public static Color BurlyWood { get { return new Color (0xFFDEB887); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF5F9EA0 (cadet blue).</summary>
		public static Color CadetBlue { get { return new Color (0xFF5F9EA0); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF7FFF00 (chartreuse).</summary>
		public static Color Chartreuse { get { return new Color (0xFF7FFF00); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFD2691E (chocolate).</summary>
		public static Color Chocolate { get { return new Color (0xFFD2691E); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFF7F50 (coral).</summary>
		public static Color Coral { get { return new Color (0xFFFF7F50); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF6495ED (cornflower blue).</summary>
		public static Color CornflowerBlue { get { return new Color (0xFF6495ED); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFF8DC (cornsilk).</summary>
		public static Color Cornsilk { get { return new Color (0xFFFFF8DC); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFDC143C (crimson).</summary>
		public static Color Crimson { get { return new Color (0xFFDC143C); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF00FFFF (cyan).</summary>
		public static Color Cyan { get { return new Color (0xFF00FFFF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF00008B (dark blue).</summary>
		public static Color DarkBlue { get { return new Color (0xFF00008B); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF008B8B (dark cyan).</summary>
		public static Color DarkCyan { get { return new Color (0xFF008B8B); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFB8860B (dark goldenrod).</summary>
		public static Color DarkGoldenrod { get { return new Color (0xFFB8860B); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF444444 (dark gray).</summary>
		public static Color DarkGray { get { return new Color (0xFF444444); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF006400 (dark green).</summary>
		public static Color DarkGreen { get { return new Color (0xFF006400); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFBDB76B (dark khaki).</summary>
		public static Color DarkKhaki { get { return new Color (0xFFBDB76B); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF8B008B (dark magenta).</summary>
		public static Color DarkMagenta { get { return new Color (0xFF8B008B); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF556B2F (dark olive green).</summary>
		public static Color DarkOliveGreen { get { return new Color (0xFF556B2F); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFF8C00 (dark orange).</summary>
		public static Color DarkOrange { get { return new Color (0xFFFF8C00); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF9932CC (dark orchid).</summary>
		public static Color DarkOrchid { get { return new Color (0xFF9932CC); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF8B0000 (dark red).</summary>
		public static Color DarkRed { get { return new Color (0xFF8B0000); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFE9967A (dark salmon).</summary>
		public static Color DarkSalmon { get { return new Color (0xFFE9967A); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF8FBC8B (dark sea green).</summary>
		public static Color DarkSeaGreen { get { return new Color (0xFF8FBC8B); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF483D8B (dark slate blue).</summary>
		public static Color DarkSlateBlue { get { return new Color (0xFF483D8B); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF2F4F4F (dark slate gray).</summary>
		public static Color DarkSlateGray { get { return new Color (0xFF2F4F4F); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF00CED1 (dark turquoise).</summary>
		public static Color DarkTurquoise { get { return new Color (0xFF00CED1); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF9400D3 (dark violet).</summary>
		public static Color DarkViolet { get { return new Color (0xFF9400D3); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFF1493 (deep pink).</summary>
		public static Color DeepPink { get { return new Color (0xFFFF1493); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF00BFFF (deep sky blue).</summary>
		public static Color DeepSkyBlue { get { return new Color (0xFF00BFFF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF696969 (dim gray).</summary>
		public static Color DimGray { get { return new Color (0xFF696969); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF1E90FF (dodger blue).</summary>
		public static Color DodgerBlue { get { return new Color (0xFF1E90FF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFB22222 (firebrick).</summary>
		public static Color Firebrick { get { return new Color (0xFFB22222); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFFAF0 (floral white).</summary>
		public static Color FloralWhite { get { return new Color (0xFFFFFAF0); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF228B22 (forest green).</summary>
		public static Color ForestGreen { get { return new Color (0xFF228B22); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFF00FF (fuchsia).</summary>
		public static Color Fuchsia { get { return new Color (0xFFFF00FF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFDCDCDC (gainsboro).</summary>
		public static Color Gainsboro { get { return new Color (0xFFDCDCDC); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF8F8FF (ghost white).</summary>
		public static Color GhostWhite { get { return new Color (0xFFF8F8FF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFD700 (gold).</summary>
		public static Color Gold { get { return new Color (0xFFFFD700); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFDAA520 (goldenrod).</summary>
		public static Color Goldenrod { get { return new Color (0xFFDAA520); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF888888 (gray).</summary>
		public static Color Gray { get { return new Color (0xFF888888); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF00FF00 (green).</summary>
		public static Color Green { get { return new Color (0xFF00FF00); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFADFF2F (green-yellow).</summary>
		public static Color GreenYellow { get { return new Color (0xFFADFF2F); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF0FFF0 (honeydew).</summary>
		public static Color Honeydew { get { return new Color (0xFFF0FFF0); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFF69B4 (hot pink).</summary>
		public static Color HotPink { get { return new Color (0xFFFF69B4); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFCD5C5C (indian red).</summary>
		public static Color IndianRed { get { return new Color (0xFFCD5C5C); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF4B0082 (indigo).</summary>
		public static Color Indigo { get { return new Color (0xFF4B0082); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFFFF0 (ivory).</summary>
		public static Color Ivory { get { return new Color (0xFFFFFFF0); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF0E68C (khaki).</summary>
		public static Color Khaki { get { return new Color (0xFFF0E68C); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFE6E6FA (lavender).</summary>
		public static Color Lavender { get { return new Color (0xFFE6E6FA); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFF0F5 (lavender blush).</summary>
		public static Color LavenderBlush { get { return new Color (0xFFFFF0F5); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF7CFC00 (lawn green).</summary>
		public static Color LawnGreen { get { return new Color (0xFF7CFC00); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFFACD (lemon chiffon).</summary>
		public static Color LemonChiffon { get { return new Color (0xFFFFFACD); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFADD8E6 (light blue).</summary>
		public static Color LightBlue { get { return new Color (0xFFADD8E6); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF08080 (light coral).</summary>
		public static Color LightCoral { get { return new Color (0xFFF08080); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFE0FFFF (light cyan).</summary>
		public static Color LightCyan { get { return new Color (0xFFE0FFFF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFAFAD2 (light goldenrod yellow).</summary>
		public static Color LightGoldenrodYellow { get { return new Color (0xFFFAFAD2); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF90EE90 (light green).</summary>
		public static Color LightGreen { get { return new Color (0xFF90EE90); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFCCCCCC (light gray).</summary>
		public static Color LightGray { get { return new Color (0xFFCCCCCC); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFB6C1 (light pink).</summary>
		public static Color LightPink { get { return new Color (0xFFFFB6C1); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFA07A (light salmon).</summary>
		public static Color LightSalmon { get { return new Color (0xFFFFA07A); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF20B2AA (light sea green).</summary>
		public static Color LightSeaGreen { get { return new Color (0xFF20B2AA); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF87CEFA (light sky blue).</summary>
		public static Color LightSkyBlue { get { return new Color (0xFF87CEFA); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF778899 (light slate gray).</summary>
		public static Color LightSlateGray { get { return new Color (0xFF778899); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFB0C4DE (light steel blue).</summary>
		public static Color LightSteelBlue { get { return new Color (0xFFB0C4DE); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFFFE0 (light yellow).</summary>
		public static Color LightYellow { get { return new Color (0xFFFFFFE0); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF00FF00 (lime).</summary>
		public static Color Lime { get { return new Color (0xFF00FF00); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF32CD32 (lime green).</summary>
		public static Color LimeGreen { get { return new Color (0xFF32CD32); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFAF0E6 (linen).</summary>
		public static Color Linen { get { return new Color (0xFFFAF0E6); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFF00FF (magenta).</summary>
		public static Color Magenta { get { return new Color (0xFFFF00FF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF800000 (maroon).</summary>
		public static Color Maroon { get { return new Color (0xFF800000); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF66CDAA (medium aquamarine).</summary>
		public static Color MediumAquamarine { get { return new Color (0xFF66CDAA); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF0000CD (medium blue).</summary>
		public static Color MediumBlue { get { return new Color (0xFF0000CD); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFBA55D3 (medium orchid).</summary>
		public static Color MediumOrchid { get { return new Color (0xFFBA55D3); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF9370DB (medium purple).</summary>
		public static Color MediumPurple { get { return new Color (0xFF9370DB); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF3CB371 (medium sea green).</summary>
		public static Color MediumSeaGreen { get { return new Color (0xFF3CB371); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF7B68EE (medium slate blue).</summary>
		public static Color MediumSlateBlue { get { return new Color (0xFF7B68EE); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF00FA9A (medium spring green).</summary>
		public static Color MediumSpringGreen { get { return new Color (0xFF00FA9A); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF48D1CC (medium turquoise).</summary>
		public static Color MediumTurquoise { get { return new Color (0xFF48D1CC); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFC71585 (medium violet-red).</summary>
		public static Color MediumVioletRed { get { return new Color (0xFFC71585); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF191970 (midnight blue).</summary>
		public static Color MidnightBlue { get { return new Color (0xFF191970); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF5FFFA (mint cream).</summary>
		public static Color MintCream { get { return new Color (0xFFF5FFFA); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFE4E1 (misty rose).</summary>
		public static Color MistyRose { get { return new Color (0xFFFFE4E1); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFE4B5 (moccasin).</summary>
		public static Color Moccasin { get { return new Color (0xFFFFE4B5); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFDEAD (navajo white).</summary>
		public static Color NavajoWhite { get { return new Color (0xFFFFDEAD); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF000080 (navy).</summary>
		public static Color Navy { get { return new Color (0xFF000080); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFDF5E6 (old lace).</summary>
		public static Color OldLace { get { return new Color (0xFFFDF5E6); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF808000 (olive).</summary>
		public static Color Olive { get { return new Color (0xFF808000); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF6B8E23 (olive drab).</summary>
		public static Color OliveDrab { get { return new Color (0xFF6B8E23); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFA500 (orange).</summary>
		public static Color Orange { get { return new Color (0xFFFFA500); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFF4500 (orange-red).</summary>
		public static Color OrangeRed { get { return new Color (0xFFFF4500); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFDA70D6 (orchid).</summary>
		public static Color Orchid { get { return new Color (0xFFDA70D6); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFEEE8AA (pale goldenrod).</summary>
		public static Color PaleGoldenrod { get { return new Color (0xFFEEE8AA); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF98FB98 (pale green).</summary>
		public static Color PaleGreen { get { return new Color (0xFF98FB98); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFAFEEEE (pale turquoise).</summary>
		public static Color PaleTurquoise { get { return new Color (0xFFAFEEEE); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFDB7093 (pale violet-red).</summary>
		public static Color PaleVioletRed { get { return new Color (0xFFDB7093); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFEFD5 (papaya whip).</summary>
		public static Color PapayaWhip { get { return new Color (0xFFFFEFD5); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFDAB9 (peach puff).</summary>
		public static Color PeachPuff { get { return new Color (0xFFFFDAB9); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFCD853F (peru).</summary>
		public static Color Peru { get { return new Color (0xFFCD853F); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFC0CB (pink).</summary>
		public static Color Pink { get { return new Color (0xFFFFC0CB); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFDDA0DD (plum).</summary>
		public static Color Plum { get { return new Color (0xFFDDA0DD); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFB0E0E6 (powder blue).</summary>
		public static Color PowderBlue { get { return new Color (0xFFB0E0E6); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF800080 (purple).</summary>
		public static Color Purple { get { return new Color (0xFF800080); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFF0000 (red).</summary>
		public static Color Red { get { return new Color (0xFFFF0000); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFBC8F8F (rosy brown).</summary>
		public static Color RosyBrown { get { return new Color (0xFFBC8F8F); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF4169E1 (royal blue).</summary>
		public static Color RoyalBlue { get { return new Color (0xFF4169E1); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF8B4513 (saddle brown).</summary>
		public static Color SaddleBrown { get { return new Color (0xFF8B4513); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFA8072 (salmon).</summary>
		public static Color Salmon { get { return new Color (0xFFFA8072); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF4A460 (sandy brown).</summary>
		public static Color SandyBrown { get { return new Color (0xFFF4A460); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF2E8B57 (sea green).</summary>
		public static Color SeaGreen { get { return new Color (0xFF2E8B57); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFF5EE (sea shell).</summary>
		public static Color SeaShell { get { return new Color (0xFFFFF5EE); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFA0522D (sienna).</summary>
		public static Color Sienna { get { return new Color (0xFFA0522D); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFC0C0C0 (silver).</summary>
		public static Color Silver { get { return new Color (0xFFC0C0C0); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF87CEEB (sky blue).</summary>
		public static Color SkyBlue { get { return new Color (0xFF87CEEB); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF6A5ACD (slate blue).</summary>
		public static Color SlateBlue { get { return new Color (0xFF6A5ACD); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF708090 (slate gray).</summary>
		public static Color SlateGray { get { return new Color (0xFF708090); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFFAFA (snow).</summary>
		public static Color Snow { get { return new Color (0xFFFFFAFA); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF00FF7F (spring green).</summary>
		public static Color SpringGreen { get { return new Color (0xFF00FF7F); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF4682B4 (steel blue).</summary>
		public static Color SteelBlue { get { return new Color (0xFF4682B4); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFD2B48C (tan).</summary>
		public static Color Tan { get { return new Color (0xFFD2B48C); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF008080 (teal).</summary>
		public static Color Teal { get { return new Color (0xFF008080); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFD8BFD8 (thistle).</summary>
		public static Color Thistle { get { return new Color (0xFFD8BFD8); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFF6347 (tomato).</summary>
		public static Color Tomato { get { return new Color (0xFFFF6347); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF40E0D0 (turquoise).</summary>
		public static Color Turquoise { get { return new Color (0xFF40E0D0); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFEE82EE (violet).</summary>
		public static Color Violet { get { return new Color (0xFFEE82EE); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF5DEB3 (wheat).</summary>
		public static Color Wheat { get { return new Color (0xFFF5DEB3); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFFFFF (white).</summary>
		public static Color White { get { return new Color (0xFFFFFFFF); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFF5F5F5 (white smoke).</summary>
		public static Color WhiteSmoke { get { return new Color (0xFFF5F5F5); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FFFFFF00 (yellow).</summary>
		public static Color Yellow { get { return new Color (0xFFFFFF00); } }
		/// <summary>Gets a <see cref="Color" /> with the ARGB value #FF9ACD32 (yellow-green).</summary>
		public static Color YellowGreen { get { return new Color (0xFF9ACD32); } }
	}

	public class ColorValueMarshaler : JniValueMarshaler<Color>
	{
		const string ExpressionRequiresUnreferencedCode = "System.Linq.Expression usage may trim away required code.";

		public override Type MarshalType {
			get { return typeof (int); }
		}

		public override Color CreateGenericValue (ref JniObjectReference reference, JniObjectReferenceOptions options, Type? targetType)
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

		[RequiresDynamicCode (ExpressionRequiresUnreferencedCode)]
		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateParameterToManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue, ParameterAttributes synchronize, Type? targetType)
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

		[RequiresDynamicCode (ExpressionRequiresUnreferencedCode)]
		[RequiresUnreferencedCode (ExpressionRequiresUnreferencedCode)]
		public override Expression CreateReturnValueFromManagedExpression (JniValueMarshalerContext context, ParameterExpression sourceValue)
		{
			return CreateParameterFromManagedExpression (context, sourceValue, 0);
		}
	}
}
