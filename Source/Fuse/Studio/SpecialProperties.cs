
using System.Reactive.Subjects;
using Outracks.Fuse.Live;
using Outracks.Fuse.Model;
using Outracks.Fusion;

namespace Outracks.Fuse
{
	public static class SpecialProperties
	{
		public static IAttribute UxName(this IElement element)
		{
			return element["ux:Name"];
		}

		public static IAttribute UxKey(this IElement element)
		{
			return element["ux:Key"];
		}

		public static IAttribute UxProperty(this IElement element)
		{
			return element["ux:Property"];
		}
		public static BehaviorSubject<string> UxProperty(this ElementModel element)
		{
			return element["ux:Property"];
		}
		public static IAttribute UxValue(this IElement element)
		{
			return element["ux:Value"];
		}

		public static IAttribute UxClass(this IElement element)
		{
			return element["ux:Class"];
		}

	
		public static BehaviorSubject<string> UxClass(this ElementModel element)
		{
			return element["ux:Class"];
		}

		public static IAttribute UxInnerClass(this IElement element)
		{
			return element["ux:InnerClass"];
		}

		public static BehaviorSubject<string> UxInnerClass(this ElementModel element)
		{
			return element["ux:InnerClass"];
		}

		public static IAttribute UxGlobal(this IElement element)
		{
			return element["ux:Global"];
		}
		public static BehaviorSubject<string> UxGlobal(this ElementModel element)
		{
			return element["ux:Global"];
		}
	}
}
