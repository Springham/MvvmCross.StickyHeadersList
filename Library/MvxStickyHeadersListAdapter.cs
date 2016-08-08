using Android.Content;
using Android.Views;
using MvvmCross.Binding.Droid.BindingContext;
using MvvmCross.Binding.Droid.Views;
using MvvmCross.StickyHeadersList.Interfaces;

namespace MvvmCross.StickyHeadersList
{
    public abstract class MvxStickyHeadersListAdapter : MvxAdapter, IMvxStickyHeadersListAdapter
    {
        protected MvxStickyHeadersListAdapter(Context activity, IMvxAndroidBindingContext bindingContext)
                    : base(activity, bindingContext)
        {
        }

        public abstract View GetHeaderView(int position, View convertView, ViewGroup parent);

        public abstract long GetHeaderId(int position);
    }
}