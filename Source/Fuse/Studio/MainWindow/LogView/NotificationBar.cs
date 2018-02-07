using System;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using Fuse.Preview;
using Outracks.Fuse.Designer;
using Outracks.Fusion;

namespace Outracks.Fuse
{
	class NotificationBar : IStatus
	{
		readonly IProperty<bool> _logViewIsExpanded;
		readonly IScheduler _scheduler;

		readonly BehaviorSubject<Brush> _background = new BehaviorSubject<Brush>(Brush.Transparent);
		readonly BehaviorSubject<Brush> _foreground = new BehaviorSubject<Brush>(Brush.Transparent);
		readonly BehaviorSubject<string> _message = new BehaviorSubject<string>("");
		readonly BehaviorSubject<string> _details = new BehaviorSubject<string>("");
		readonly BehaviorSubject<Option[]> _options = new BehaviorSubject<Option[]>(new Option[0]);
		readonly BehaviorSubject<bool> _show = new BehaviorSubject<bool>(false);
		readonly BehaviorSubject<bool> _busy = new BehaviorSubject<bool>(false);

		public NotificationBar(IProperty<bool> logViewIsExpanded, IScheduler scheduler)
		{
			_logViewIsExpanded = logViewIsExpanded;
			_scheduler = scheduler;
		}

		public IControl Control
		{
			get
			{
				var showAfter250Ms = _show.DistinctUntilChanged()
					.Throttle(TimeSpan.FromMilliseconds(250))
					.Merge(_show.Where(s => s == false));

				return Layout.Dock()
					.Right(BusyIndicator(_foreground.Switch())
						.WithHeight(8).WithWidth(52)
						.CenterVertically()
						.WithPadding(new Thickness<Points>(0, 0, 8, 0))
						.ShowWhen(_busy))
					.Right(Spacer.Small)
					.Right(_options.Select(opts => 
						opts.Select(cmd =>
							Buttons.NotificationButton(cmd.Text, _scheduler.CreateCommand(cmd.Action), _background.Switch())
								.WithWidth(80)
								.WithHeight(18)
								.Center()))
						.StackFromLeft(separator: () => Spacer.Small))
					.Right(Spacer.Small)
					.Right(Label.Create(_message.AsText(), Theme.DescriptorFont, color: _foreground.Switch())
						.CenterVertically())
					.Fill()
					.WithBackground(Shapes.Rectangle(fill: _background.Switch()))
					.WithHeight(24)
					.OnMouse(Command.Enabled(() => _logViewIsExpanded.Write(true)))
					.MakeCollapsable(RectangleEdge.Top, showAfter250Ms, lazy: false);
			}
		}

		static IControl BusyIndicator(IObservable<Color> overlaycolor)
		{
			return Image.Animation("FuseBusyAnim", typeof(LogoAndVersion).Assembly, TimeSpan.FromMilliseconds(1000), overlaycolor);
		}

		public void Busy(string message, params Option[] options)
		{
			_background.OnNext(Theme.ReifyBarBackground);
			_foreground.OnNext(Theme.BuildBarForeground);
			_message.OnNext(message);
			_details.OnNext("");
			_options.OnNext(options);
			_busy.OnNext(true);
			_show.OnNext(true);
		}

		public void Error(string message, params Option[] options)
		{
			Error(message, "", options);
		}

		public void Error(string message, string details, params Option[] options)
		{
			_background.OnNext(Theme.ErrorColor);
			_foreground.OnNext(Color.White);
			_message.OnNext(message);
			_details.OnNext("");
			_options.OnNext(options);
			_busy.OnNext(false);
			_show.OnNext(true);
		}

		public void Ready()
		{
			_show.OnNext(false);
		}
	}
}