using Android.Content;
using Android.Widget;

namespace MvvmCross.StickyHeadersList
{
    /// <summary>
    /// Wrapper for section indexer
    /// </summary>
    public class MvxSectionIndexerAdapterWrapper : MvxAdapterWrapper, ISectionIndexer
    {
        private readonly ISectionIndexer _mSectionIndexer;

        public MvxSectionIndexerAdapterWrapper(Context context, ISectionIndexer indexer) 
            : base(context, indexer)
        {
            _mSectionIndexer = indexer;
        }


        public int GetPositionForSection(int section)
        {
            return _mSectionIndexer.GetPositionForSection(section);
        }

        public int GetSectionForPosition(int position)
        {
            return _mSectionIndexer.GetSectionForPosition(position);
        }

        public Java.Lang.Object[] GetSections()
        {
            return _mSectionIndexer.GetSections();
        }
    }
}