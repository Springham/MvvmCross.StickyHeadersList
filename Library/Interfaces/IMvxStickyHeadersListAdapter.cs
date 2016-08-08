using Android.Views;
using MvvmCross.Binding.Droid.Views;

namespace MvvmCross.StickyHeadersList.Interfaces
{
    public interface IMvxStickyHeadersListAdapter : IMvxAdapter
    {
        /// <summary>
        /// Get a View that displays the header data at the specified position in the
        /// set. You can either create a view manually or inflate it from an XML layout file
        /// </summary>
        /// <param name="position">The position of the item within the adapter's data set of the item whose
        /// header view we want.</param>
        /// <param name="convertView">The old view to reuse, if possible. Note: you shoudl check that this is
        /// non-null and of an approriate type before using. If it is not possible to convert the view
        /// to display the correct data this method can create a new view</param>
        /// <param name="parent">The parent that this view will evenaually attach to</param>
        /// <returns>A view corresponding to the data at the specified position</returns>
        View GetHeaderView(int position, View convertView, ViewGroup parent);

        /// <summary>
        /// Get the header id associated with the specificed position in the list
        /// </summary>
        /// <param name="position">The position of the item within the adapter's data set whose header id we want</param>
        /// <returns>The id of the header at the specified position</returns>
        long GetHeaderId(int position);
    }
}