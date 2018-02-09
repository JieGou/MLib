namespace FolderBrowser.Views
{
    using FileSystemModels;
    using FolderBrowser.Interfaces;
    using System.Windows.Controls;

    /// <summary>
    /// Interaction logic for FolderBrowserTreeView.xaml
    /// </summary>
    public partial class FolderBrowserTreeView : UserControl
    {
        /// <summary>
        /// Standard class constructor
        /// </summary>
        public FolderBrowserTreeView()
        {
            InitializeComponent();
            Loaded += FolderBrowserTreeView_Loaded;
        }

        /// <summary>
        /// Initializes the folder browser viewmodel and view as soon as the view is loaded.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void FolderBrowserTreeView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            Loaded -= FolderBrowserTreeView_Loaded;

            var vm = DataContext as IBrowserViewModel;

            if (vm != null)
                vm.BrowsePath(vm.InitialPath);
            else
            {
                if (DataContext != null)
                    System.Console.WriteLine("FolderBrowserTreeView: Attached vm is: {0}", DataContext.ToString());
                else
                {
                    System.Console.WriteLine("FolderBrowserTreeView: No Vm Attached!");
                    this.DataContextChanged += FolderBrowserTreeView_DataContextChangedAsync;
                }
            }
        }

        private async void FolderBrowserTreeView_DataContextChangedAsync(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            this.DataContextChanged -= FolderBrowserTreeView_DataContextChangedAsync;

            var vm = e.NewValue as IBrowserViewModel;

            if (vm != null)
            {
                if (string.IsNullOrEmpty(vm.InitialPath) == false)
                {
                    System.Console.WriteLine("FolderBrowserTreeView: Browsing Path on DataContextChanged: '{0}'", vm.InitialPath);

                    await vm.NavigateToAsync(PathFactory.Create(vm.InitialPath));
                }
            }
        }
    }
}
