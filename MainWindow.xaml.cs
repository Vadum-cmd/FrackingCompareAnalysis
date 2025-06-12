using System.Windows.Navigation;

namespace FrekingCompareAnalysis
{
    public partial class MainWindow : NavigationWindow
    {
        public MainWindow()
        {
            InitializeComponent();
            this.Navigate(new Pages.MainPage());
        }
    }
}