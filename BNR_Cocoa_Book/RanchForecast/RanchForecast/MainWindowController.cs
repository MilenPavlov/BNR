﻿using System;

using Foundation;
using AppKit;
using System.Threading.Tasks;
using WebKit;
using CoreGraphics;
using ObjCRuntime;
using System.Runtime.InteropServices;

namespace RanchForecast
{
	public partial class MainWindowController : NSWindowController, INSTableViewSource
    {
		ScheduleFetcher scheduleFetcher;
//		NSPanel webPanel;
		NSWindow webPanel;
		WebView webView;
		NSProgressIndicator progressBar;
		double progress = 0;

        public MainWindowController(IntPtr handle) : base(handle)
        {
        }

        [Export("initWithCoder:")]
        public MainWindowController(NSCoder coder) : base(coder)
        {
        }

        public MainWindowController() : base("MainWindow")
        {
        }

        public override async void AwakeFromNib()
        {
            base.AwakeFromNib();
			scheduleFetcher = new ScheduleFetcher();
			await scheduleFetcher.FetchClassesAsync();
			tableView.ReloadData();
			// Crypto error inducing? Check in XM 1.13... still crashing
//			tableView.DoubleClick += TableView_DoubleClick;
			// Try this instead.
			// And again avoiding event handlers avoids the crypto native crash. 
			// OK, seems to work best when the Selector for the Action is Obj-C style, 
			// i.e. let it pass one paramter (so selector ends in colon), object sender,
			// and don't Export but rather mark it as an Action
			// Have selector C# method take an IntPtr and convert if need be with:
			// UILabel (or whatever) label = Runtime.GetNSObject<UILabel> (intPtr);
			tableView.Target = this;
			tableView.DoubleAction = new Selector("tableViewDoubleClick:");
        }

		[Action("tableViewDoubleClick:")]
		public void TableViewDoubleClick(NSObject sender)
		{
//			NSTableView tv = (NSTableView)sender;
//			Console.WriteLine("TableView: {0}", tv);
			ScheduledClass c = scheduleFetcher.ScheduledClasses[(int)tableView.ClickedRow];

//			webPanel = new NSPanel();
			webPanel = new NSWindow();
			webPanel.StyleMask = NSWindowStyle.Resizable | webPanel.StyleMask;
			webPanel.SetContentSize(new CGSize(Window.ContentView.Frame.Size.Width, 500.0f));
			webView = new WebView(new CGRect(0.0f, 50.0f, Window.ContentView.Frame.Size.Width, 450.0f), "", "");
			webView.AutoresizingMask = NSViewResizingMask.HeightSizable | NSViewResizingMask.WidthSizable;
			webPanel.ContentView.AddSubview(webView);

			webView.WeakResourceLoadDelegate = this;
			webView.WeakFrameLoadDelegate = this;

			progressBar = new NSProgressIndicator(new CGRect(25.0f, 12.0f, Window.ContentView.Frame.Size.Width-175.0f, 25.0f));
			progressBar.Style = NSProgressIndicatorStyle.Bar;
			progressBar.Indeterminate = false;
			progressBar.AutoresizingMask = NSViewResizingMask.WidthSizable;
			webPanel.ContentView.AddSubview(progressBar);
			progressBar.MinValue = 0;
			progressBar.MaxValue = 100;
			progressBar.DoubleValue = progress;

			NSButton closebutton = new NSButton(new CGRect(webPanel.Frame.Width - 125.0f, 12.0f, 100.0f, 25.0f));
			closebutton.Title = "Close";
			closebutton.BezelStyle = NSBezelStyle.Rounded;
			closebutton.Target = this;
			closebutton.Action = new Selector("closePanel:");
			closebutton.AutoresizingMask = NSViewResizingMask.MinXMargin;
			webPanel.DefaultButtonCell = closebutton.Cell;
			webPanel.ContentView.AddSubview(closebutton);

			webView.MainFrameUrl = c.Href;
//			Window.BeginSheet(webPanel, (nint) => {
//
//			});
			NSApplication.SharedApplication.BeginSheet(webPanel, Window);

			//NSWorkspace.SharedWorkspace.OpenUrl(new NSUrl(c.Href));
		}

//        public void TableView_DoubleClick (object sender, EventArgs e)
//        {
//			NSTableView tv = sender as NSTableView;
//			ScheduledClass c = scheduleFetcher.ScheduledClasses[(int)tv.ClickedRow];
//
//			webPanel = new NSPanel();
//			webPanel.SetContentSize(new CGSize(Window.ContentView.Frame.Size.Width, 500.0f));
//			webView = new WebView(new CGRect(0.0f, 0.0f, Window.ContentView.Frame.Size.Width, 500.0f), "", "");
//			webPanel.ContentView.AddSubview(webView);
//			webView.ResourceLoadDelegate = new MyWebResourceLoadDelegate();
//			webView.FrameLoadDelegate = new MyWebFrameLoadDelegate();
//
//			NSButton closebutton = new NSButton(new CGRect(webPanel.Frame.Width/2 - 62.0f, 0.0f, 100.0f, 25.0f));
//			closebutton.Title = "Close";
//			closebutton.BezelStyle = NSBezelStyle.Rounded;
//			closebutton.Target = this;
//			closebutton.Action = new Selector("closePanel");
//			webPanel.DefaultButtonCell = closebutton.Cell;
//			webPanel.ContentView.AddSubview(closebutton);
//
//			webView.MainFrameUrl = c.Href;
//			Window.BeginSheet(webPanel, (i) => {
//
//			});
//
//			//NSWorkspace.SharedWorkspace.OpenUrl(new NSUrl(c.Href));
//        }

		[Action("closePanel:")]
		public void ClosePanel (NSObject sender) 
		{
//			NSButton button = (NSButton)sender;
//			Console.WriteLine("Button: {0}", button);
//			Window.EndSheet(webPanel);
			NSApplication.SharedApplication.EndSheet(webPanel);
			webPanel.OrderOut(sender);
			webView.Dispose();
			webView = null;
			webPanel.Dispose();
			webPanel = null;
		}

        public new MainWindow Window
        {
            get { return (MainWindow)base.Window; }
        }

		[Export("numberOfRowsInTableView:")]
		public System.nint GetRowCount(AppKit.NSTableView tableView)
		{
			return scheduleFetcher.ScheduledClasses.Count;
		}

		[Export("tableView:objectValueForTableColumn:row:")]
		public Foundation.NSObject GetObjectValue(AppKit.NSTableView tableView, AppKit.NSTableColumn tableColumn, System.nint row)
		{
			ScheduledClass cl = scheduleFetcher.ScheduledClasses[(int)row];

			if (tableColumn.Identifier != "Begin") {
				return cl.ValueForKey(new NSString(tableColumn.Identifier));
			}
			else {
				DateTime date = DateTime.Parse(cl.ValueForKey(new NSString(tableColumn.Identifier)).ToString()).ToUniversalTime();
				// Manually make NSString with desired date format to pass to cell
//				return new NSString(date.ToLongDateString());
				// Convert DateTime to NSDate to pass to cell and use Date Formatter for cell.
				date = DateTime.SpecifyKind(date, DateTimeKind.Local);
				return (NSDate)date;
			}
		}


		[Export("webView:didStartProvisionalLoadForFrame:")]
		public void StartedProvisionalLoad(WebView sender, WebFrame forFrame)
		{
			Console.WriteLine("Load Started:");
			InvokeOnMainThread(() => {
				progress = 0;
				progressBar.DoubleValue = progress;
			});
		}

		[Export("webView:didFinishLoadForFrame:")]
		public void FinishedLoad(WebView sender, WebFrame forFrame)
		{
			Console.WriteLine("Load Finished");
			InvokeOnMainThread(() => {
				progress = 100;
				progressBar.DoubleValue = progress;
			});

		}

		[Export("webView:didFailLoadWithError:forFrame:")]
		public void FailedLoadWithError(WebView sender, NSError error, WebFrame forFrame)
		{
			Console.WriteLine("Load Failed: {0}", error.Description);
		}

		[Export("webView:resource:didReceiveContentLength:fromDataSource:")]
		public void OnReceivedContentLength(WebView sender, NSObject identifier, nint length, WebDataSource dataSource)
		{
			Console.WriteLine("OnReceivedContentLength: {0}", length);
			InvokeOnMainThread(() => {
				progressBar.IncrementBy(2.5);
			});
		}

		[Export("webView:resource:didReceiveResponse:fromDataSource:")]
		public void OnReceivedResponse(WebView sender, NSObject identifier, NSUrlResponse responseReceived, WebDataSource dataSource)
		{
			Console.WriteLine("OnReceivedResponse: {0}", responseReceived.ExpectedContentLength);
		}
    }
}
