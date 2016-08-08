using System;
using System.Collections.Generic;
using Android.Content;
using Android.Database;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using Java.Lang;
using MvvmCross.Binding.Droid.Views;
using MvvmCross.StickyHeadersList.Interfaces;
using Exception = System.Exception;
using Math = System.Math;

namespace MvvmCross.StickyHeadersList
{
    public class MvxStickyHeadersListView : MvxListView, AbsListView.IOnScrollListener
    {
        public IOnScrollListener OnScrollListenerDelegate { get; set; }

        private bool _mAreHeadersSticky = true;

        /// <summary>
        ///     gets or sets if the headers are sticky
        /// </summary>
        public bool AreHeadersSticky
        {
            get { return _mAreHeadersSticky; }
            set
            {
                if (_mAreHeadersSticky == value)
                    return;

                _mAreHeadersSticky = value;
                RequestLayout();
            }
        }

        private IOnHeaderListClickListener _mOnHeaderListClickListener;

        public IOnHeaderListClickListener OnHeaderListClickListener
        {
            get { return _mOnHeaderListClickListener; }
            set
            {
                _mOnHeaderListClickListener = value;
                _mAdapterHeaderAdapterClickListener = new AdapterHeaderAdapterClickListener(_mOnHeaderListClickListener, this);
            }
        }

        public bool IsDrawingListUnderStickyHeader { get; set; }

        private int _mHeaderBottomPosition;
        private View _mHeader;
        private int _mDividerHeight;
        private Drawable _mDivider;
        private bool _mClippingToPadding;
        private readonly Rect _mClippingRect = new Rect();
        private long _mCurrentHeaderId = -1;
        private MvxAdapterWrapper _mAdapter;
        private float _mHeaderDownY = -1;
        private bool _mHeaderBeingPressed;
        private int _mHeaderPosition;
        private ViewConfiguration _mViewConfiguration;
        private List<View> _mFooterViews;
        private Rect _mSelectorRect = new Rect(); //for if reflection fails
        private IntPtr _mSelectorPositionField;
        private AdapterHeaderAdapterClickListener _mAdapterHeaderAdapterClickListener;
        private DataSetObserver _mDataSetObserver;
        private bool _initialized;

        private class AdapterHeaderAdapterClickListener : IOnHeaderAdapterClickListener
        {
            private readonly IOnHeaderListClickListener m_OnHeaderListClickListener;
            private readonly MvxStickyHeadersListView _mStickyListHeadersListView;

            public AdapterHeaderAdapterClickListener(IOnHeaderListClickListener listClickListener,
                MvxStickyHeadersListView stickyListHeadersListView)
            {
                m_OnHeaderListClickListener = listClickListener;
                _mStickyListHeadersListView = stickyListHeadersListView;
            }

            public void OnHeaderClick(View header, int itemPosition, long headerId)
            {
                m_OnHeaderListClickListener?.OnHeaderClick(_mStickyListHeadersListView, header, itemPosition, headerId, false);
            }
        }

        private class StickyListHeadersListViewObserver : DataSetObserver
        {
            private readonly MvxStickyHeadersListView m_ListView;

            public StickyListHeadersListViewObserver(MvxStickyHeadersListView listView)
            {
                m_ListView = listView;
            }

            public override void OnInvalidated()
            {
                m_ListView.Reset();
            }

            public override void OnChanged()
            {
                m_ListView.Reset();
            }
        }

        public MvxStickyHeadersListView(IntPtr javaReference, JniHandleOwnership transfer)
            : base(javaReference, transfer)
        {
        }

        public MvxStickyHeadersListView(Context context, IAttributeSet attrs)
            : this(context, attrs, new MvxAdapter(context))
        {
        }

        public MvxStickyHeadersListView(Context context, IAttributeSet attrs, IMvxAdapter mvxAdapter)
            : base(context, attrs, mvxAdapter)
        {
            Initialize(context);
        }

        private void Initialize(Context context)
        {
            if (!_initialized)
            {
                _mDataSetObserver = new StickyListHeadersListViewObserver(this);
                _mAdapterHeaderAdapterClickListener = new AdapterHeaderAdapterClickListener(OnHeaderListClickListener,
                    this);

                base.SetOnScrollListener(this);
                //null out divider, dividers are handled by adapter so they look good with headers
                base.Divider = null;
                base.DividerHeight = 0;

                _mViewConfiguration = ViewConfiguration.Get(context);
                _mClippingToPadding = true;

                try
                {
                    //reflection to get selector ref
                    var absListViewClass = JNIEnv.FindClass(typeof (AbsListView));
                    var selectorRectId = JNIEnv.GetFieldID(absListViewClass, "mSelectorRect", "()Landroid/graphics/Rect");
                    var selectorRectField = JNIEnv.GetObjectField(absListViewClass, selectorRectId);
                    _mSelectorRect = GetObject<Rect>(selectorRectField, JniHandleOwnership.TransferLocalRef);

                    var selectorPositionId = JNIEnv.GetFieldID(absListViewClass, "mSelectorPosition",
                        "()Ljava/lang/Integer");
                    _mSelectorPositionField = JNIEnv.GetObjectField(absListViewClass, selectorPositionId);
                }
                catch (Exception)
                {
                }
                _initialized = true;
            }
        }

        protected override void OnFinishInflate()
        {
            base.OnFinishInflate();

            Initialize(Context);
        }

        protected override void OnLayout(bool changed, int left, int top, int right, int bottom)
        {
            base.OnLayout(changed, left, top, right, bottom);
            if (changed)
            {
                Reset();
                ScrollChanged(FirstVisiblePosition);
            }
        }

        private void Reset()
        {
            _mHeader = null;
            _mCurrentHeaderId = -1;
            _mHeaderPosition = -1;
            _mHeaderBottomPosition = -1;
        }

        public override bool PerformItemClick(View view, int position, long id)
        {
            if (view is WrapperView)
                view = ((WrapperView) view).Item;

            return base.PerformItemClick(view, position, id);
        }

        public override void SetSelectionFromTop(int position, int y)
        {
            if (HasStickyHeaderAtPosition(position))
                y += GetHeaderHeight();

            base.SetSelectionFromTop(position, y);
        }


#if __ANDROID_11__

        public override void SmoothScrollToPositionFromTop(int position, int offset)
        {
            if (HasStickyHeaderAtPosition(position))
                offset += GetHeaderHeight();

            base.SmoothScrollToPositionFromTop(position, offset);
        }

        public override void SmoothScrollToPositionFromTop(int position, int offset, int duration)
        {
            if (HasStickyHeaderAtPosition(position))
                offset += GetHeaderHeight();

            base.SmoothScrollToPositionFromTop(position, offset, duration);
        }
#endif

        private bool HasStickyHeaderAtPosition(int position)
        {
            position -= HeaderViewsCount;
            return AreHeadersSticky && position > 0 &&
                   position < _mAdapter.Count &&
                   _mAdapter.GetHeaderId(position) == _mAdapter.GetHeaderId(position - 1);
        }

        public override Drawable Divider
        {
            get { return _mDivider; }
            set
            {
                _mDivider = value;
                if (_mDivider != null)
                {
                    var dividerDrawableHeight = _mDivider.IntrinsicHeight;
                    if (dividerDrawableHeight >= 0)
                    {
                        DividerHeight = dividerDrawableHeight;
                    }
                }

                if (_mAdapter != null)
                {
                    _mAdapter.Divider = _mDivider;
                    RequestLayout();
                    Invalidate();
                }
            }
        }

        public override int DividerHeight
        {
            get { return _mDividerHeight; }
            set
            {
                _mDividerHeight = value;
                if (_mAdapter != null)
                {
                    _mAdapter.DividerHeight = _mDividerHeight;
                    RequestLayout();
                    Invalidate();
                }
            }
        }

        public override void SetOnScrollListener(IOnScrollListener l)
        {
            OnScrollListenerDelegate = l;
        }



        public new IMvxAdapter Adapter
        {
            get { return base.Adapter; }
            set
            {
                if (IsInEditMode)
                {
                    base.Adapter = value;
                    return;
                }

                if (value == null)
                {
                    _mAdapter = null;
                    Reset();
                    base.Adapter = null;
                    return;
                }

                if (!(value is IMvxStickyHeadersListAdapter))
                {
                    throw new IllegalArgumentException("Adapter must implement IStickyListHeadersAdapater");
                }

                _mAdapter = WrapAdapter(value);
                Reset();
                base.Adapter = _mAdapter;
            }
        }

        private MvxAdapterWrapper WrapAdapter(IListAdapter adapter)
        {
            var indexer = adapter as ISectionIndexer;
            var wrapper = indexer != null
                ? new MvxSectionIndexerAdapterWrapper(Context, indexer)
                : new MvxAdapterWrapper(Context, adapter);

            wrapper.Divider = _mDivider;
            wrapper.DividerHeight = _mDividerHeight;
            wrapper.RegisterDataSetObserver(_mDataSetObserver);
            wrapper.OnHeaderAdapterClickListener = _mAdapterHeaderAdapterClickListener;
            return wrapper;
        }

        public IMvxStickyHeadersListAdapter WrappedAdapter => _mAdapter?.Delegate;

        public View GetWrappedView(int position)
        {
            var view = GetChildAt(position);
            var wrapperView = view as WrapperView;

            return wrapperView != null ? wrapperView.Item : view;
        }

        protected override void DispatchDraw(Canvas canvas)
        {
            if ((int) Build.VERSION.SdkInt < 8) //froyo
            {
                ScrollChanged(FirstVisiblePosition);
            }

            PositionSelectorRect();

            if (!AreHeadersSticky || _mHeader == null)
            {
                base.DispatchDraw(canvas);
                return;
            }

            if (!IsDrawingListUnderStickyHeader)
            {
                _mClippingRect.Set(0, _mHeaderBottomPosition, Width, Height);
                canvas.Save();
                canvas.ClipRect(_mClippingRect);
            }

            base.DispatchDraw(canvas);

            if (!IsDrawingListUnderStickyHeader)
            {
                canvas.Restore();
            }

            DrawStickyHeader(canvas);
        }

        private void PositionSelectorRect()
        {
            if (_mSelectorRect.IsEmpty)
                return;

            var selectorPosition = GetSelectorPosition();
            if (selectorPosition < 0)
                return;

            var firstVisibleItem = FixedFirstVisibleItem(FirstVisiblePosition);
            var view = GetChildAt(selectorPosition - firstVisibleItem) as WrapperView;
            if (view == null)
                return;
            _mSelectorRect.Top = view.Top + view.ItemTop;
        }

        private int GetSelectorPosition()
        {
            if (_mSelectorPositionField == IntPtr.Zero) //
            {
                for (var i = 0; i < ChildCount; i++)
                {
                    if (GetChildAt(i).Bottom == _mSelectorRect.Bottom)
                        return i + FixedFirstVisibleItem(FirstVisiblePosition);
                }
            }
            else
            {
                try
                {
                    return GetObject<Integer>(_mSelectorPositionField, JniHandleOwnership.TransferLocalRef).IntValue();
                }
                catch (Exception)
                {
                }
            }


            return -1;
        }

        private void DrawStickyHeader(Canvas canvas)
        {
            var headerHeight = GetHeaderHeight();
            var top = _mHeaderBottomPosition - headerHeight;

            //clip the headers drawing area
            _mClippingRect.Left = PaddingLeft;
            _mClippingRect.Right = Width - PaddingRight;
            _mClippingRect.Bottom = top + headerHeight;
            _mClippingRect.Top = _mClippingToPadding ? PaddingTop : 0;

            canvas.Save();
            canvas.ClipRect(_mClippingRect);
            canvas.Translate(PaddingLeft, top);
            _mHeader.Draw(canvas);
            canvas.Restore();
        }

        private void MeasureHeader()
        {
            var widthMeasureSpec = MeasureSpec.MakeMeasureSpec(Width - PaddingLeft - PaddingRight - (IsScrollBarOverlay() ? 0 : VerticalScrollbarWidth),
                MeasureSpecMode.Exactly);

            int heightMeasureSpec;
            var layoutParams = _mHeader.LayoutParameters;
            if (layoutParams == null)
            {
                _mHeader.LayoutParameters = new MarginLayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.WrapContent);
            }
            if (layoutParams != null && layoutParams.Height > 0)
            {
                heightMeasureSpec = MeasureSpec.MakeMeasureSpec(layoutParams.Height, MeasureSpecMode.Exactly);
            }
            else
            {
                heightMeasureSpec = MeasureSpec.MakeMeasureSpec(0, MeasureSpecMode.Unspecified);
            }

            _mHeader.Measure(widthMeasureSpec, heightMeasureSpec);
#if __ANDROID_17__
            if ((int)Build.VERSION.SdkInt >= 17) //JB_MR1
            {
                _mHeader.LayoutDirection = LayoutDirection;
            }
#endif

            _mHeader.Layout(PaddingLeft, 0, Width - PaddingRight, _mHeader.MeasuredHeight);
        }

        private bool IsScrollBarOverlay()
        {
            return ScrollBarStyle == ScrollbarStyles.InsideOverlay || ScrollBarStyle == ScrollbarStyles.OutsideOverlay;
        }

        private int GetHeaderHeight()
        {
            return _mHeader?.MeasuredHeight ?? 0;
        }

        public override void SetClipToPadding(bool clipToPadding)
        {
            base.SetClipToPadding(clipToPadding);
            _mClippingToPadding = clipToPadding;
        }

        private void ScrollChanged(int reportedFirstVisibleItem)
        {
            var adapaterCount = _mAdapter?.Count ?? 0;
            if (adapaterCount == 0 || !AreHeadersSticky)
                return;

            var listViewHeaderCount = HeaderViewsCount;
            var firstVisibleItem = FixedFirstVisibleItem(reportedFirstVisibleItem) - listViewHeaderCount;

            if (firstVisibleItem < 0 || firstVisibleItem > adapaterCount - 1)
            {
                Reset();
                UpdateHeaderVisibilities();
                Invalidate();
                return;
            }

            if (_mHeaderPosition == -1 || _mHeaderPosition != firstVisibleItem)
            {
                _mHeaderPosition = firstVisibleItem;
                _mCurrentHeaderId = _mAdapter.GetHeaderId(firstVisibleItem);
                _mHeader = _mAdapter.GetHeaderView(_mHeaderPosition, _mHeader, this);
                MeasureHeader();
            }

            var childCount = ChildCount;
            if (childCount != 0)
            {
                View viewToWatch = null;
                var watchingChildDistance = int.MaxValue;
                var viewToWatchIsFooter = false;
                for (var i = 0; i < childCount; i++)
                {
                    var child = GetChildAt(i);
                    var childIsFooter = _mFooterViews != null && _mFooterViews.Contains(child);
                    var childDistance = child.Top - (_mClippingToPadding ? PaddingTop : 0);
                    if (childDistance < 0)
                        continue;

                    if (viewToWatch == null ||
                        (!viewToWatchIsFooter && !((WrapperView) viewToWatch).HasHeader) ||
                        ((childIsFooter || ((WrapperView) child).HasHeader) && childDistance < watchingChildDistance))
                    {
                        viewToWatch = child;
                        viewToWatchIsFooter = childIsFooter;
                        watchingChildDistance = childDistance;
                    }
                }

                var headerHeight = GetHeaderHeight();
                if (viewToWatch != null && (viewToWatchIsFooter || ((WrapperView) viewToWatch).HasHeader))
                {
                    if (firstVisibleItem == listViewHeaderCount && GetChildAt(0).Top > 0 && !_mClippingToPadding)
                    {
                        _mHeaderBottomPosition = 0;
                    }
                    else
                    {
                        var paddingTop = _mClippingToPadding ? PaddingTop : 0;
                        _mHeaderBottomPosition = Math.Min(viewToWatch.Top, headerHeight + paddingTop);
                        _mHeaderBottomPosition = _mHeaderBottomPosition < paddingTop
                            ? headerHeight + paddingTop
                            : _mHeaderBottomPosition;
                    }
                }
                else
                {
                    _mHeaderBottomPosition = headerHeight + (_mClippingToPadding ? PaddingTop : 0);
                }
            }

            UpdateHeaderVisibilities();
            Invalidate();
        }

        public override void AddFooterView(View v)
        {
            base.AddFooterView(v);
            if (_mFooterViews == null)
                _mFooterViews = new List<View>();

            _mFooterViews.Add(v);
        }

        public override bool RemoveFooterView(View v)
        {
            if (base.RemoveFooterView(v))
            {
                _mFooterViews.Remove(v);
                return true;
            }

            return false;
        }

        private void UpdateHeaderVisibilities()
        {
            var top = _mClippingToPadding ? PaddingTop : 0;
            var childCount = ChildCount;
            for (var i = 0; i < childCount; i++)
            {
                var child = GetChildAt(i) as WrapperView;
                if (child == null)
                    continue;

                if (!child.HasHeader)
                    continue;

                var childHeader = child.Header;
                childHeader.Visibility = child.Top < top ? ViewStates.Invisible : ViewStates.Visible;
            }
        }

        private int FixedFirstVisibleItem(int firstVisibileItem)
        {
            if ((int) Build.VERSION.SdkInt >= 11) //HC
            {
                return firstVisibileItem;
            }

            for (var i = 0; i < ChildCount; i++)
            {
                if (GetChildAt(i).Bottom >= 0)
                {
                    firstVisibileItem += i;
                    break;
                }
            }

            //Work around to fix bug with firstVisibileItem being to high beacuse
            //ListView does not take clipTOPadding=false into account
            if (!_mClippingToPadding && PaddingTop > 0 && GetChildAt(0).Top > 0 && firstVisibileItem > 0)
            {
                firstVisibileItem -= 1;
            }

            return firstVisibileItem;
        }

        //TODO handle touched better, multitouch etc.
        public override bool OnTouchEvent(MotionEvent e)
        {
            var action = e.Action;
            if (action == MotionEventActions.Down && e.GetY() <= _mHeaderBottomPosition)
            {
                _mHeaderDownY = e.GetY();
                _mHeaderBeingPressed = true;
                _mHeader.Pressed = true;
                _mHeader.Invalidate();
                Invalidate(0, 0, Width, _mHeaderBottomPosition);
                return true;
            }

            if (_mHeaderBeingPressed)
            {
                if (Math.Abs(e.GetY() - _mHeaderDownY) < _mViewConfiguration.ScaledTouchSlop)
                {
                    if (action == MotionEventActions.Up || action == MotionEventActions.Cancel)
                    {
                        _mHeaderDownY = -1;
                        _mHeaderBeingPressed = false;
                        _mHeader.Pressed = false;
                        _mHeader.Invalidate();
                        Invalidate(0, 0, Width, _mHeaderBottomPosition);
                        OnHeaderListClickListener?.OnHeaderClick(this, _mHeader, _mHeaderPosition, _mCurrentHeaderId, true);
                    }
                    return true;
                }

                _mHeaderDownY = -1;
                _mHeaderBeingPressed = false;
                _mHeader.Pressed = false;
                _mHeader.Invalidate();
                Invalidate(0, 0, Width, _mHeaderBottomPosition);
            }
            return base.OnTouchEvent(e);
        }


        public virtual void OnScroll(AbsListView view, int firstVisibleItem, int visibleItemCount, int totalItemCount)
        {
            OnScrollListenerDelegate?.OnScroll(view, firstVisibleItem, visibleItemCount, totalItemCount);

            if ((int) Build.VERSION.SdkInt >= 8) //FROYO
            {
                ScrollChanged(firstVisibleItem);
            }
        }

        public virtual void OnScrollStateChanged(AbsListView view, ScrollState scrollState)
        {
            OnScrollListenerDelegate?.OnScrollStateChanged(view, scrollState);
        }
    }
}