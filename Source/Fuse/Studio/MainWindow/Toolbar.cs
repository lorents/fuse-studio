using System.Linq;
using System.Reactive.Linq;
using Outracks.Fuse.Designer.Icons;
using Outracks.Fuse.Stage;
using Outracks.Fusion;
using Outracks.IPC;

namespace Outracks.Fuse
{
	class Toolbar
	{
		public static IControl Create(
			IPopover popover, IProperty<Mode> mode, 
			StageController stageController, 
			PreviewController previewController)
		{
			var isCompact = mode.Convert(m => m == Mode.Compact, m => m ? Mode.Compact : Mode.Normal);
			var toggleMode = isCompact.Toggle();

			var codeView = new CodeView(previewController.AccessCode, NetworkHelper.GetInterNetworkIps().ToArray());

			return Layout.Layer(
				Layout.StackFromLeft(	
					codeView.Create(popover)
						.HideWhen(isCompact),
							
					Control.Empty.WithWidth(16),
							
					Layout
						.StackFromLeft(CreateHeaderControl(
							icon: Fuse.Icons.AddViewport(),
							tooltipText: "Click to add a new Viewport",
							buttonText : "Add Viewport",
							command: stageController.NewViewport,
							labelColor: Theme.DefaultText),
							Control.Empty.WithWidth(16)
						)
						.HideWhen(isCompact),
								
					CreateHeaderControl(
						icon: MinimizeAndMaximizeIcon.Create(mode),
						tooltipText: "Switch between normal and compact mode. Click to switch mode.",
						buttonText : "Compact",
						labelColor: Theme.DefaultText,
						command: toggleMode)
						.HideWhen(isCompact),
	
					CreateCompactSelectionControl(mode, stageController.SelectionEnabled, toggleMode)
						.ShowWhen(isCompact)
						.Center(),
								
					Control.Empty.WithWidth(4))
					.DockRight(),
				CreateFullSelectionControl(stageController.SelectionEnabled)
					.HideWhen(isCompact)
					.CenterHorizontally())
				.WithHeight(37)
				.WithPadding(new Thickness<Points>(8, 0, 8, 0))
				.WithBackground(Theme.PanelBackground);
		}

		
		static IControl CreateFullSelectionControl(IProperty<bool> selectionEnabled)
		{
			return Layout.StackFromLeft(
				Layout.Dock()
					.Bottom(
						Shapes.Rectangle(
							fill: selectionEnabled.IsFalse()
								.Select(e => e ? Color.Transparent : Theme.Active)
								.Switch())
							.WithHeight(1))
					.Fill(
						CreateHeaderControl(
							icon: SelectionIcon.Create(selectionEnabled, true),
							tooltipText: "Enable to select elements in the app.",
							buttonText : "Selection",
							labelColor: selectionEnabled.IsFalse()
								.Select(e => e ? Theme.DefaultText : Theme.ActiveHover)
								.Switch(),
							command: selectionEnabled.Toggle())),
				Control.Empty.WithWidth(24),
				Layout.Dock()
					.Bottom(
						Shapes.Rectangle(
							fill: selectionEnabled.IsFalse()
								.Select(e => e ? Theme.Active : Color.Transparent)
								.Switch())
							.WithHeight(1))
					.Fill(
						CreateHeaderControl(
							icon: TouchIcon.Create(selectionEnabled, true),
							tooltipText: "Enable to intract with the app.",
							buttonText : "Touch",
							labelColor: selectionEnabled
								.Select(e => e ? Theme.DefaultText : Theme.ActiveHover)
								.Switch(),
							command: selectionEnabled.Toggle())));
		}
				
		static IControl CreateHeaderControl(
			Command command,
			string buttonText,
			string tooltipText,
			IControl icon,
			Brush labelColor)
		{
			return Button.Create(command, state => 
				Layout.StackFromLeft(
					Control.Empty.WithWidth(4),
					icon,
					Control.Empty.WithWidth(4),
					Label.Create(
						text: buttonText,
						color:labelColor,
						font: Theme.DescriptorFont)
						.CenterVertically(),
					Control.Empty.WithWidth(4))
					.SetToolTip(tooltipText)
					.WithBackground(
						background: Observable.CombineLatest(
							state.IsEnabled, state.IsHovered,
							(enabled, hovering) =>
								hovering
									? Theme.FaintBackground
									: Color.Transparent)
							.Switch()));
		}
		
		
		static IControl CreateCompactSelectionControl(
			IProperty<Mode> mode,
			IProperty<bool> selectionEnabled,
			Command toggleMode)
		{
			return Layout.StackFromLeft(
				Button.Create(selectionEnabled.Toggle(), state =>
					Layout.Dock()
						.Bottom(
							Shapes.Rectangle(
								fill: Theme.Active)
								.WithSize(new Size<Points>(1, 1)))
						.Fill(
							Layout.StackFromLeft(
								SelectionIcon.Create(selectionEnabled, true)
									.OnMouse(pressed: selectionEnabled.Toggle())
									.ShowWhen(selectionEnabled),
								TouchIcon.Create(selectionEnabled, true)
									.Center()
									.OnMouse(pressed: selectionEnabled.Toggle())
									.ShowWhen(selectionEnabled.IsFalse())))
						.WithPadding(new Thickness<Points>(4, 0, 4, 0))
						.WithBackground(
							background: Observable.CombineLatest(
								state.IsEnabled, state.IsHovered,
								(enabled, hovering) =>
									hovering
										? Theme.FaintBackground
										: Color.Transparent)
								.Switch())
						.SetToolTip("Enable to select elements in the app. Disable to interact with the app.")),
				Control.Empty.WithWidth(8),
				Button.Create(toggleMode, state =>
					MinimizeAndMaximizeIcon.Create(mode)
						.WithPadding(new Thickness<Points>(4, 0, 4, 0))
						.WithBackground(
							background: Observable.CombineLatest(
								state.IsEnabled, state.IsHovered,
								(enabled, hovering) =>
									hovering
										? Theme.FaintBackground
										: Color.Transparent)
								.Switch()))
					.SetToolTip("Switch between normal and compact mode. Click to switch mode."));
		}
	}
}