namespace Outracks.Fuse.Inspector.Editors
{
	using Fusion;
	
	static class SliderEditor
	{
		public static IControl Create(IAttribute value, double min, double max)
		{
			return Control.Empty;
			//return Slider.Create(value.ScrubValue, min, max)
			//	.WithHeight(CellLayout.DefaultCellHeight);
		}
	}
}