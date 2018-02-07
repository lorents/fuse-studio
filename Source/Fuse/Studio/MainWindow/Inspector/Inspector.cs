using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using Outracks.Fuse.Editing;
using Outracks.Fuse.Live;

namespace Outracks.Fuse.Inspector
{
	using Fusion;
	using Simulator;
	using Editors;

	public class Inspector
	{
		public static readonly string Title = "Inspector";
		public static readonly Points Width = 295;

		public static IControl Create(ContextController context, PreviewController previewController, IScheduler scheduler)
		{
			var modelUpdater = new ModelUpdater(previewController);
			var insertElement = new InsertElement(modelUpdater);
			var removeElement = new RemoveElement(modelUpdater);
			var cutCopyPaste = new CutCopyPaste(removeElement);

			var element = context.CurrentSelection
				.Select(el => 
					el.IsUnknown
						? Element.Empty
						: new ElementEditor(
							el,
							previewController.Metadata,
							previewController,
							cutCopyPaste,
							insertElement, 
							removeElement,
							scheduler))
				.Switch();

			var elementChanged = Observable.Return(new object()); //element.SimulatorId.Select(id => (object)id);


			return Popover.Host(popover =>
			{
				var uneditableElementMessage = UneditableElementMessage(element);
				var uneditableElementIsSelected = uneditableElementMessage.Select(x => x.HasValue);
				var uneditablePlaceholder = UneditablePlaceholder(uneditableElementMessage).ShowWhen(uneditableElementIsSelected);

				return Layout.StackFromTop(
						Sections.CommonSection.Create(element, context.Project, new Factory(elementChanged, popover), popover),
						Sections.AdvancedSection.Create(element, new Factory(elementChanged, popover))
							//.MakeCollapsable(RectangleEdge.Top, uneditableElementIsSelected.IsFalse(), animate: false)
						)
					.WithWidth(Width)
					.DockLeft()
					.MakeScrollable(darkTheme: Theme.IsDark, horizontalScrollBarVisible: false)
					.WithBackground(uneditablePlaceholder.ShowWhen(uneditableElementIsSelected))
					.WithOverlay(Placeholder().ShowWhen(element.IsEmpty));
			});
		}

		static IControl Placeholder()
		{
			Points rectangleWidth = 100;
			Points rectangleHeight = 30;

			var rectangles = Layout.StackFromTop(
				Shapes.Rectangle(
						fill: Theme.Shadow,
						cornerRadius: Observable.Return(new CornerRadius(2)))
					.WithSize(new Size<Points>(rectangleWidth * 0.8, rectangleHeight))
					.DockLeft(),
				Spacer.Small,
				Shapes.Rectangle(
						stroke: Theme.SelectionStroke(Observable.Return(false), Observable.Return(true), Observable.Return(false)),
						fill: Theme.PanelBackground,
						cornerRadius: Observable.Return(new CornerRadius(2)))
					.WithSize(new Size<Points>(rectangleWidth, rectangleHeight))
					.DockLeft(),
				Spacer.Small,
				Shapes.Rectangle(
						fill: Theme.Shadow,
						cornerRadius: Observable.Return(new CornerRadius(2)))
					.WithSize(new Size<Points>(rectangleWidth * 0.6, rectangleHeight))
					.DockLeft());

			return Layout.StackFromTop(
					rectangles.CenterHorizontally()
						.WithOverlay(Arrow().WithPadding(new Thickness<Points>(rectangleWidth * 0.99, rectangleHeight * 1.3, 0, 0)).Center()),
					Spacer.Medium,
					Label.Create(
						"Select something to get started",
						color: Theme.DefaultText,
						font: Theme.DefaultFont))
				.Center()
				.WithBackground(Shapes.Rectangle(fill: Theme.PanelBackground))
				.MakeHittable()
				.Control;
		}

		static IObservable<Optional<string>> UneditableElementMessage(IElement currentSelection)
		{
			return currentSelection.Is("Fuse.Triggers.Trigger")
				.CombineLatest(currentSelection.Is("Fuse.Animations.Animator"),
					(isTrigger, isAnimator) =>
					{
						if (isTrigger || isAnimator)
							return Optional.Some(string.Format("Currently you can't edit {0}.\r\nYou'll have to do it manually.", isTrigger ? "Triggers" : "Animators"));
						return Optional.None();
					})
				.DistinctUntilChanged()
				.Replay(1)
				.RefCount();
		}

		static IControl UneditablePlaceholder(IObservable<Optional<string>> uneditableElementMessage)
		{
			return Layout.StackFromTop(
					Icons.CannotEditPlaceholder().WithPadding(bottom: new Points(20)),
					Label.Create(
						text: uneditableElementMessage.Select(x => x.OrDefault()).AsText(),
						font: Theme.DefaultFont,
						color: Theme.DisabledText))
				.Center();
		}

		static IControl Arrow()
		{
			var enabledIcon = Image.FromResource("Outracks.Fuse.Icons.selection_icon_on.png", typeof(Inspector).Assembly, overlayColor: Theme.PanelBackground);
			var disabledIcon = Image.FromResource("Outracks.Fuse.Icons.selection_icon_off.png", typeof(Inspector).Assembly, overlayColor: Theme.FieldFocusStroke.Brush);
			return disabledIcon.WithBackground(enabledIcon).WithSize(new Size<Points>(30, 30));
		}

	}


	static class Rows
	{
		public static IControl NameRow(this IEditorFactory editors, string name, IAttribute property, bool deferEdit = false)
		{
			return Layout.Dock()
				.Left(editors.Label(name, property).WithWidth(CellLayout.FullCellWidth))
				.Left(Spacer.Small)
				.Fill(editors.Field(property, placeholderText: "Add name here", deferEdit: deferEdit));
		}
	}
}
