﻿using System;
using Android.App;
using Android.Widget;
using Android.Views;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Android.Graphics;
using Android.Util;
using Android.Preferences;
using Android.OS;
using Android.Content;

namespace PhotoGallery
{
    public class PhotoGalleryFragment : Fragment
    {
		private static readonly string TAG = "PhotoGalleryFragment";

		#region = member variables
		public GridView mGridView;
		List<GalleryItem> galleryItems;
		int currentPage;
		bool endReached;
		string query;
		string lastQuery;
		#endregion

		#region - Lifecycle methods
		public override async void OnCreate(Android.OS.Bundle savedInstanceState)
		{
//			Console.WriteLine("[{0}] OnCreate Called: {1}", TAG, DateTime.Now.ToLongTimeString());
			base.OnCreate(savedInstanceState);
			RetainInstance = true;
			SetHasOptionsMenu(true);

			currentPage = 1;
			endReached = false;

			await UpdateItems();

//			Console.WriteLine("[{0}] OnCreate Done: {1}", TAG, DateTime.Now.ToLongTimeString());
		}

		public override Android.Views.View OnCreateView(Android.Views.LayoutInflater inflater, Android.Views.ViewGroup container, Android.OS.Bundle savedInstanceState)
		{
//			Console.WriteLine("[{0}] OnCreateView Called: {1}", TAG, DateTime.Now.ToLongTimeString());
			View v = inflater.Inflate(Resource.Layout.fragment_photo_gallery, container, false);

			mGridView = v.FindViewById<GridView>(Resource.Id.gridView);

			mGridView.Scroll += async (object sender, AbsListView.ScrollEventArgs e) => {
				if (e.FirstVisibleItem + e.VisibleItemCount == e.TotalItemCount && !endReached && e.TotalItemCount > 0) {
					endReached = true;
//					Console.WriteLine("[{0}] Scroll Ended", TAG);
					currentPage++;

					List<GalleryItem> newItems;
					if (query != null) {
						newItems = await new FlickrFetchr().Search(query, currentPage.ToString());
					}
					else {
						newItems = await new FlickrFetchr().Fetchitems(currentPage.ToString());
					}

					galleryItems = galleryItems.Concat(newItems).ToList();

					var adapter = (ArrayAdapter)mGridView.Adapter;
					adapter.AddAll(newItems);
					adapter.NotifyDataSetChanged();

					endReached = false;
				}
			};

			mGridView.ScrollStateChanged += (object sender, AbsListView.ScrollStateChangedEventArgs e) => {
				var adapter = (ArrayAdapter)mGridView.Adapter;
				if (e.ScrollState == ScrollState.Idle) {
					Task.Run(async () => {
						await new FlickrFetchr().PreloadImages(mGridView.FirstVisiblePosition, mGridView.LastVisiblePosition, galleryItems);
					});
				}
			};

			SetupAdapter();
//			Console.WriteLine("[{0}] OnCreateView Done: {1}", TAG, DateTime.Now.ToLongTimeString());
			return v;
		}

		public override void OnCreateOptionsMenu(IMenu menu, MenuInflater inflater)
		{
			base.OnCreateOptionsMenu(menu, inflater);
			inflater.Inflate(Resource.Menu.fragment_photo_gallery, menu);
			// Using SearchView - had issues clearing it. 
			if (Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb) {
				IMenuItem searchItem = menu.FindItem(Resource.Id.menu_item_search);
				SearchView searchView = (SearchView)searchItem.ActionView;

				// Get the data from our searchable.xml as a SearchableInfo
				SearchManager searchManager = (SearchManager)Activity.GetSystemService(Context.SearchService);
				ComponentName name = Activity.ComponentName;
				SearchableInfo searchInfo = searchManager.GetSearchableInfo(name);
				searchView.SetSearchableInfo(searchInfo);
				searchView.QueryTextFocusChange += (object sender, View.FocusChangeEventArgs e) => {
					if (e.HasFocus && lastQuery != null && lastQuery != String.Empty) {
						searchView.SetQuery(lastQuery, false);
					}
				};
				searchView.Close += async (object sender, SearchView.CloseEventArgs e) => {
					e.Handled = false;
					PreferenceManager.GetDefaultSharedPreferences(Activity).Edit().PutString(FlickrFetchr.PREF_SEARCH_QUERY, null).Commit();
					query = null;
					currentPage = 1;
					await UpdateItems();
				};
			}
		}

		public override bool OnOptionsItemSelected(IMenuItem item)
		{
			switch (item.ItemId) {
				case Resource.Id.menu_item_search:
					currentPage = 1;
					Activity.OnSearchRequested();
					return true;
				case Resource.Id.menu_item_clear:
					PreferenceManager.GetDefaultSharedPreferences(Activity).Edit().PutString(FlickrFetchr.PREF_SEARCH_QUERY, null).Commit();
					// Using SearchView - Not sure if this is correct to kill the SearchView
//					if (Build.VERSION.SdkInt >= BuildVersionCodes.Honeycomb) {
//						SearchView searchView = (SearchView)item.ActionView;
//						searchView.SetSearchableInfo(null);
//					}
					currentPage = 1;
					Task.Run(() => {
					}).ContinueWith(async(t) => {
						await UpdateItems();
					}, TaskScheduler.FromCurrentSynchronizationContext());
					return true;
				default:
					return base.OnOptionsItemSelected(item);
			}
		} 

		#endregion

		#region - Adapter
		public async Task UpdateItems() {
			if (this.Activity == null)
				return;
			ProgressDialog pg = new ProgressDialog(Activity);
			pg.SetMessage("This may take a minute or two");
			pg.SetTitle("Loading Images");
			pg.SetCancelable(false);
			pg.Show();

			query = PreferenceManager.GetDefaultSharedPreferences(Activity).GetString(FlickrFetchr.PREF_SEARCH_QUERY, null);
			if (query != null && query != String.Empty)
				lastQuery = query;
			FlickrFetchr fetchr = new FlickrFetchr();
			if (query != null) {
				galleryItems = await fetchr.Search(query, currentPage.ToString());
			}
			else {
				galleryItems = await fetchr.Fetchitems(currentPage.ToString());
			}
//			foreach (GalleryItem item in galleryItems) {
//				Console.WriteLine("[{0}]\nPhoto Id: {1}\nCaption: {2}\nUrl: {3}", TAG, item.Id, item.Caption, item.Url);
//			}
			SetupAdapter();
			Toast.MakeText(Activity, String.Format("Results: {0}", fetchr.NumberOfHits), ToastLength.Long).Show();

			pg.Dismiss();
		}

		void SetupAdapter() {
			if (Activity == null || mGridView == null) return;

			if (galleryItems != null) {
				var adapter = new GalleryItemAdapter(Activity, galleryItems);
				mGridView.Adapter = adapter;
				Task.Run(async () => {
					Display display = Activity.WindowManager.DefaultDisplay;
					DisplayMetrics outMetrics = new DisplayMetrics();
					display.GetMetrics(outMetrics);

					float density = Activity.Resources.DisplayMetrics.Density;
					var height = outMetrics.HeightPixels / density;
					var width = outMetrics.WidthPixels / density;

					int itemsPerRow = (int)Math.Round(width/120);
					int numRows = (int)Math.Round(height/120);
					int numItems = itemsPerRow * numRows;

					await new FlickrFetchr().PreloadImages(0, numItems, galleryItems).ConfigureAwait(false);
				});
			}
			else {
				mGridView.Adapter = null;
			}

		}

    }

	public class GalleryItemAdapter : ArrayAdapter<GalleryItem>
	{
		Activity context;

		public GalleryItemAdapter(Activity context, List<GalleryItem> items) : base(context, 0 , items)
		{
			this.context = context;
		}

		public override View GetView(int position, View convertView, ViewGroup parent)
		{
			// Recycle
//			View view = convertView;
//			if (view == null) {
//				view = context.LayoutInflater.Inflate(Resource.Layout.gallery_item, parent, false);
//			}

			// Don't recycle
			View view = context.LayoutInflater.Inflate(Resource.Layout.gallery_item, parent, false);

			ImageView imageView = view.FindViewById<ImageView>(Resource.Id.gallery_item_imageView);
			imageView.SetImageResource(Resource.Drawable.face_icon);

			GalleryItem item = GetItem(position);
			Task.Run(async () => {
				Bitmap image = await new FlickrFetchr().GetImageBitmapAsync(item.Url, position).ConfigureAwait(false);
				context.RunOnUiThread(() => {
					imageView.SetImageBitmap(image);
				});
			});

			return view;
		}
	}
	#endregion
}
