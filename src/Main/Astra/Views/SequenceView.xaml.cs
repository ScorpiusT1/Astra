using System.Windows.Controls;

namespace Astra.Views
{
    /// <summary>
    /// SequenceView.xaml 的交互逻辑
    /// </summary>
    public partial class SequenceView : UserControl
    {
        private ViewModels.SequenceViewModel _currentViewModel;
      
        public SequenceView()
        {
            InitializeComponent();                  
        }
    }
}
