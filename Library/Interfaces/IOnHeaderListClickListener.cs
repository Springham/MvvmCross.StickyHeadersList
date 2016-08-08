using Android.Views;

namespace MvvmCross.StickyHeadersList.Interfaces
{
    public interface IOnHeaderListClickListener
    {
        void OnHeaderClick(MvxStickyHeadersListView listView, View header, int itemPosition, long headerId, bool currentlySticky);
    }
}