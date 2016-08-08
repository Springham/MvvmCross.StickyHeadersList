using Android.Content;
using Android.Widget;

namespace MvvmCross.StickyHeadersList
{
    /// <summary>
    /// A WrapperView that implements the checkable interface
    /// </summary>
    public class CheckableWrapperView : WrapperView, ICheckable
    {

        public CheckableWrapperView(Context context) : base(context)
        {

        }

        /// <summary>
        /// Gets or sets the checked property
        /// </summary>
        public bool Checked
        {
            get { return ((ICheckable)Item).Checked; }
            set { ((ICheckable)Item).Checked = value; }
        }

        /// <summary>
        /// Toggle checked
        /// </summary>
        public void Toggle()
        {
            Checked = !Checked;
        }
    }
}