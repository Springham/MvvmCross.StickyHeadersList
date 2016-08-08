using System;
using System.Collections;
using System.Collections.Generic;
using Android.Content;
using Android.Database;
using Android.Graphics.Drawables;
using Android.Views;
using Android.Widget;
using Java.Lang;
using MvvmCross.Binding.Droid.BindingContext;
using MvvmCross.Binding.Droid.Views;
using MvvmCross.Droid.Shared.Fragments;
using MvvmCross.StickyHeadersList.Interfaces;
using Object = Java.Lang.Object;

namespace MvvmCross.StickyHeadersList
{
    /// <summary>
    ///     A ListAdapater which wraps a StickyListHeadersAdapter and automatically handles
    ///     wrapping the result of StickyListHeadersAdapter.GetView and StickyListAdapter.GetGetHeaderView
    /// </summary>
    public class MvxAdapterWrapper : MvxAdapter, IMvxStickyHeadersListAdapter
    {
        private readonly Context _mContext;

        private readonly List<View> _mHeaderCache = new List<View>();

        public MvxAdapterWrapper(Context context, object adapterDelegate)
            : base(context,new MvxAndroidBindingContext(context, new MvxSimpleLayoutInflaterHolder(new MvxLayoutInflater(context))))
        {
            _mContext = context;
            Delegate = adapterDelegate as IMvxStickyHeadersListAdapter;

            if (Delegate == null)
            {
                throw new NullReferenceException("Adapter Delegate must be of type IMvxStickyHeadersListAdapter");
            }

            Delegate.RegisterDataSetObserver(new AdapterWrapperObserver(this, _mHeaderCache));
        }

        public IMvxStickyHeadersListAdapter Delegate { get; set; }
        public IOnHeaderAdapterClickListener OnHeaderAdapterClickListener { get; set; }

        /// <summary>
        ///     Gets or sets the divider
        /// </summary>
        public Drawable Divider { get; set; }

        /// <summary>
        ///     Gets or sets the divider height
        /// </summary>
        public int DividerHeight { get; set; }
        
        public View GetHeaderView(int position, View convertView, ViewGroup parent)
        {
            return Delegate.GetHeaderView(position, convertView, parent);
        }

        public long GetHeaderId(int position)
        {
            return Delegate.GetHeaderId(position);
        }

        /// <summary>
        ///     Will recycle header from WrapperView if it exists
        /// </summary>
        /// <param name="wrapperView">wrapper view where header exists</param>
        private void RecycleHeaderIfExists(WrapperView wrapperView)
        {
            var header = wrapperView.Header;
            if (header != null)
                _mHeaderCache.Add(header);
        }

        /// <summary>
        ///     Get a header view. This optionally pulls a header from the supplied
        ///     Wrapper view and will also recycle it if it exists
        /// </summary>
        /// <param name="wrapperView">Wrapper view to pull header from</param>
        /// <param name="position">Position of the header</param>
        /// <returns>New Header view</returns>
        private View ConfigureHeader(WrapperView wrapperView, int position)
        {
            var header = wrapperView.Header ?? PopHeader();
            header = Delegate.GetHeaderView(position, header, wrapperView);
            if (header == null)
                throw new NullPointerException("Header view must not be null.");

            header.Clickable = true;
            header.Click += (sender, args) =>
            {
                if (OnHeaderAdapterClickListener == null)
                    return;

                var headerId = Delegate.GetHeaderId(position);
                OnHeaderAdapterClickListener.OnHeaderClick((View) sender, position, headerId);
            };

            return header;
        }

        /// <summary>
        ///     get the bottom of the header cache and remove
        /// </summary>
        /// <returns></returns>
        private View PopHeader()
        {
            if (_mHeaderCache.Count <= 0)
            {
                return null;
            }

            var header = _mHeaderCache[0];
            _mHeaderCache.RemoveAt(0);
            return header;
        }

        /// <summary>
        ///     Checks if the previous position has the same header ID.
        /// </summary>
        /// <param name="position">Position to check</param>
        /// <returns>True if it does</returns>
        private bool PreviousPositionHasSameHeader(int position)
        {
            return position != 0 && Delegate.GetHeaderId(position) == Delegate.GetHeaderId(position - 1);
        }

        public class AdapterWrapperObserver : DataSetObserver
        {
            private readonly MvxAdapterWrapper _mWrapper;
            public readonly List<View> MHeaderCache;

            public AdapterWrapperObserver(MvxAdapterWrapper wrapper, List<View> headerCache)
            {
                _mWrapper = wrapper;
                MHeaderCache = headerCache;
            }

            public override void OnInvalidated()
            {
                MHeaderCache.Clear();
                _mWrapper.Super_NotifyDataSetInvalidated();
            }

            public override void OnChanged()
            {
                _mWrapper.Super_NotifyDataSetChanged();
            }
        }

        #region Delegate Overrides

        public override bool AreAllItemsEnabled()
        {
            return Delegate.AreAllItemsEnabled();
        }

        public override bool IsEnabled(int position)
        {
            return Delegate.IsEnabled(position);
        }

        public override int Count => Delegate.Count;

        public override Object GetItem(int position)
        {
            return Delegate.GetItem(position);
        }

        public override long GetItemId(int position)
        {
            return Delegate.GetItemId(position);
        }

        public override bool HasStableIds => Delegate == null || Delegate.HasStableIds;

        public override int GetItemViewType(int position)
        {
            return Delegate.GetItemViewType(position);
        }

        public override int ViewTypeCount => Delegate.ViewTypeCount;

        public override bool IsEmpty => Delegate.IsEmpty;

        public override View GetView(int position, View convertView, ViewGroup parent)
        {
            var wrapperView = (convertView == null) ? new WrapperView(_mContext) : convertView as WrapperView;

            if (wrapperView == null)
            {
                throw new NullReferenceException("Wrapper view can not be null");
            }

            var item = Delegate.GetView(position, wrapperView.Item, wrapperView);
            View header = null;
            if (PreviousPositionHasSameHeader(position))
            {
                RecycleHeaderIfExists(wrapperView);
            }
            else
            {
                header = ConfigureHeader(wrapperView, position);
            }

            if ((item is ICheckable) && !(wrapperView is CheckableWrapperView))
            {
                //Need to create Checkable subclass of WrapperView for listview to work correctly
                wrapperView = new CheckableWrapperView(_mContext);
            }
            else if (!(item is ICheckable) && (wrapperView is CheckableWrapperView))
            {
                wrapperView = new WrapperView(_mContext);
            }

            wrapperView.Update(item, header, Divider, DividerHeight);
            return wrapperView;
        }

        public override bool Equals(Object o)
        {
            return Delegate.Equals(o);
        }

        public override View GetDropDownView(int position, View convertView, ViewGroup parent)
        {
            return ((BaseAdapter) Delegate).GetDropDownView(position, convertView, parent);
        }

        public override int GetHashCode()
        {
            return Delegate.GetHashCode();
        }

        public override void NotifyDataSetChanged()
        {
            ((BaseAdapter) Delegate).NotifyDataSetChanged();
        }

        public void Super_NotifyDataSetChanged()
        {
            base.NotifyDataSetChanged();
        }

        public override void NotifyDataSetInvalidated()
        {
            ((BaseAdapter) Delegate).NotifyDataSetInvalidated();
        }

        public void Super_NotifyDataSetInvalidated()
        {
            base.NotifyDataSetInvalidated();
        }

        public override string ToString()
        {
            return Delegate.ToString();
        }

        #endregion

        #region IMvxAdapter implementation

        object IMvxAdapter.GetRawItem(int position)
        {
            return Delegate.GetRawItem(position);
        }

        int IMvxAdapter.GetPosition(object value)
        {
            return Delegate.GetPosition(value);
        }

        int IMvxAdapter.SimpleViewLayoutId
        {
            get { return Delegate.SimpleViewLayoutId; }
            set { Delegate.SimpleViewLayoutId = value; }
        }

        IEnumerable IMvxAdapter.ItemsSource
        {
            get { return Delegate.ItemsSource; }
            set { Delegate.ItemsSource = value; }
        }

        int IMvxAdapter.ItemTemplateId
        {
            get { return Delegate.ItemTemplateId; }
            set { Delegate.ItemTemplateId = value; }
        }

        int IMvxAdapter.DropDownItemTemplateId
        {
            get { return Delegate.DropDownItemTemplateId; }
            set { Delegate.DropDownItemTemplateId = value; }
        }

        #endregion
    }
}