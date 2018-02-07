using System;

namespace Outracks.Fuse
{
	public static class BooleanParser
	{
		public static IProperty<bool> AsBoolean(this IProperty<string> property, bool defaultValue)
		{
			return property.Convert(str => TryParseBoolean(str).Value.Or(defaultValue), b => b.ToString());
		}

		public static IAttribute GetBoolean(this IElement element, string property, bool defaultValue)
		{
			return element[property]
				//.Convert(
				//parse: TryParseBoolean,
				//serialize: t => t.ToString(),
				//defaultValue: defaultValue)
				;
		}

		static Parsed<bool> TryParseBoolean(string str)
		{
			bool value;
			if (Boolean.TryParse(str, out value))
				return Parsed.Success(value, str);

			return Parsed.Failure<bool>(str);
		}
	}
}