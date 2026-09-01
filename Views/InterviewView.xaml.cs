using System.Windows.Controls;
using Assistant.ViewModels;

namespace Assistant.Views
{
    public partial class InterviewView : UserControl
    {
        public InterviewView(InterviewViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
