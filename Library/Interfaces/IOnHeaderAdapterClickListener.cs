using Android.Views;

namespace MvvmCross.StickyHeadersList.Interfaces
{
    public interface IOnHeaderAdapterClickListener
    {
        void OnHeaderClick(View header, int itemPosition, long headerId);
    }
}