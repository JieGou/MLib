﻿namespace FolderBrowser.ViewModels
{
    using FileSystemModels;
    using FileSystemModels.Browse;
    using FileSystemModels.Events;
    using FileSystemModels.Interfaces;
    using FileSystemModels.Interfaces.Bookmark;
    using FileSystemModels.Models.FSItems;
    using FileSystemModels.Models.FSItems.Base;
    using FileSystemModels.ViewModels.Base;
    using FolderBrowser.Interfaces;
    using FolderBrowser.ViewModels.Messages;
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Threading;
    using WPFProcessingLib;
    using WPFProcessingLib.Interfaces;

    /// <summary>
    /// A browser viewmodel is about managing activities and properties related
    /// to displaying a treeview that repesents folders in the file system.
    /// 
    /// This viewmodel is almost equivalent to the backend code needed to drive
    /// the Treeview that shows the items in the UI.
    /// </summary>
    internal class BrowserViewModel : ViewModelBase, IBrowserViewModel
    {
        #region fields
        private string _SelectedFolder;
        private bool _IsSpecialFoldersVisisble;

        private ICommand _ExpandCommand;
        private ICommand _FolderSelectedCommand = null;
        private ICommand _SelectedFolderChangedCommand;
        private ICommand _OpenInWindowsCommand = null;
        private ICommand _CopyPathCommand = null;
        private ICommand _RenameCommand;
        private ICommand _StartRenameCommand;
        private ICommand _CreateFolderCommand;
        private ICommand _CancelBrowsingCommand;
        private ICommand _RefreshViewCommand;

        private bool _IsBrowsing;
        private IProcessViewModel _Processor = null;
        private readonly IProcessViewModel _ExpandProcessor = null;

        private bool _IsExpanding = false;

        private string _InitalPath;
        private bool _UpdateView;
        private bool _IsBrowseViewEnabled;
        private ITreeItemViewModel _SelectedItem = null;
        private SortableObservableDictionaryCollection _Root;
        private ObservableCollection<ICustomFolderItemViewModel> _SpecialFolders;
        private bool _IsExternallyBrowsing;
        #endregion fields

        #region constructor
        /// <summary>
        /// Standard class constructor
        /// </summary>
        public BrowserViewModel()
        {
            DisplayMessage = new DisplayMessageViewModel();
            BookmarkFolder = new EditFolderBookmarks();
            InitializeSpecialFolders();

            _OpenInWindowsCommand = null;
            _CopyPathCommand = null;

            _Root = new SortableObservableDictionaryCollection();

            _ExpandProcessor = ProcessFactory.CreateProcessViewModel();
            _Processor = ProcessFactory.CreateProcessViewModel();

            InitialPath = string.Empty;

            _UpdateView = true;
            _IsBrowseViewEnabled = true;

            _IsBrowsing = true;
            _IsExternallyBrowsing = false;
        }
        #endregion constructor

        #region browsing events
        /// <summary>
        /// Indicates when the viewmodel starts heading off somewhere else
        /// and when its done browsing to a new location.
        /// </summary>
        public event EventHandler<BrowsingEventArgs> BrowseEvent;
        #endregion browsing events

        #region properties
        /// <summary>
        /// This property determines whether the control
        /// is to be updated right now or not. Switching off updates at times
        /// can save performance when browsing long and deep paths with multiple
        /// levels - so we:
        /// 1) Switch off view updates
        /// 2) Browse the structure to a target
        /// 3) Switch on updates and update view at current/new location.
        /// </summary>
        public bool UpdateView
        {
            get
            {
                return _UpdateView;
            }

            set
            {
                if (_UpdateView != value)
                {
                    _UpdateView = value;
                    RaisePropertyChanged(() => UpdateView);
                }
            }
        }

        public bool IsBrowseViewEnabled
        {
            get
            {
                return _IsBrowseViewEnabled;
            }

            set
            {
                if (_IsBrowseViewEnabled != value)
                {
                    _IsBrowseViewEnabled = value;
                    RaisePropertyChanged(() => IsBrowseViewEnabled);
                }
            }
        }

        /// <summary>
        /// Gets whether the tree browser is currently processing
        /// a request for brwosing to a known location.
        /// 
        /// Can only be set by the control if user started browser process
        /// 
        /// Use IsBrowsing and IsExternallyBrowsing to lock the controls UI
        /// during browse operations or display appropriate progress bar(s).
        /// </summary>
        public bool IsBrowsing
        {
            get
            {
                return _IsBrowsing;
            }

            private set
            {
                if (_IsBrowsing != value)
                {
                    _IsBrowsing = value;
                    RaisePropertyChanged(() => IsBrowsing);
                }
            }
        }

        /// <summary>
        /// This should only be set by the controller that started the browser process.
        /// 
        /// Use IsBrowsing and IsExternallyBrowsing to lock the controls UI
        /// during browse operations or display appropriate progress bar(s).
        /// </summary>
        public bool IsExternallyBrowsing
        {
            get
            {
                return _IsExternallyBrowsing;
            }

            protected set
            {
                if (_IsExternallyBrowsing != value)
                {
                    _IsExternallyBrowsing = value;
                    RaisePropertyChanged(() => IsExternallyBrowsing);
                }
            }
        }

        /// <summary>
        /// Gets the list of drives and folders for display in treeview structure control.
        /// </summary>
        public IEnumerable<ITreeItemViewModel> Root
        {
            get
            {
                return _Root;
            }
        }

        /// <summary>
        /// Get/set currently selected folder.
        /// 
        /// This property is used as output of the current path
        /// but also used as a parameter when browsing to a new path.
        /// </summary>
        public string SelectedFolder
        {
            get
            {
                return this._SelectedFolder;
            }

            set
            {
                if (this._SelectedFolder != value)
                {
                    this._SelectedFolder = value;
                    this.RaisePropertyChanged(() => this.SelectedFolder);
                }
            }
        }

        /// <summary>
        /// Gets the currently selected viewmodel object (if any).
        /// </summary>
        public ITreeItemViewModel SelectedItem
        {
            get
            {
                return _SelectedItem;
            }

            private set
            {
                if (_SelectedItem != value)
                {
                    _SelectedItem = value;
                    RaisePropertyChanged(() => SelectedItem);

                    if (_SelectedItem != null)
                        SelectedFolder = _SelectedItem.ItemPath;
                    else
                        SelectedFolder = string.Empty;
                }
            }
        }

        /// <summary>
        /// Gets a command to cancel the current browsing process.
        /// </summary>
        public ICommand CancelBrowsingCommand
        {
            get
            {
                if (_CancelBrowsingCommand == null)
                {
                    _CancelBrowsingCommand = new RelayCommand<object>((p) =>
                    {
                        if (_Processor != null)
                        {
                            if (_Processor.IsCancelable == true)
                                _Processor.Cancel();
                        }
                    },
                    (p) =>
                    {
                        if (IsBrowsing == true)
                        {
                            if (_Processor.IsCancelable == true)
                                return _Processor.IsProcessing;
                        }

                        return false;
                    });
                }

                return _CancelBrowsingCommand;
            }
        }

        /// <summary>
        /// Gets a command that will open the selected item with the current default application
        /// in Windows. The selected item (path to a file) is expected as FSItemVM parameter.
        /// (eg: Item is HTML file -> Open in Windows starts the web browser for viewing the HTML
        /// file if thats the currently associated Windows default application.
        /// </summary>
        public ICommand OpenInWindowsCommand
        {
            get
            {
                if (_OpenInWindowsCommand == null)
                    _OpenInWindowsCommand = new RelayCommand<object>(
                      (p) =>
                      {
                          var vm = p as ITreeItemViewModel;

                          if (vm == null)
                              return;

                          if (string.IsNullOrEmpty(vm.ItemPath) == true)
                              return;

                          FileSystemCommands.OpenContainingFolder(vm.ItemPath);
                      });

                return _OpenInWindowsCommand;
            }
        }

        /// <summary>
        /// Gets a command that will copy the path of an item into the Windows Clipboard.
        /// The item (path to a file) is expected as FSItemVM parameter.
        /// </summary>
        public ICommand CopyPathCommand
        {
            get
            {
                if (_CopyPathCommand == null)
                    _CopyPathCommand = new RelayCommand<object>(
                      (p) =>
                      {
                          var vm = p as ITreeItemViewModel;

                          if (vm == null)
                              return;

                          if (string.IsNullOrEmpty(vm.ItemPath) == true)
                              return;

                          FileSystemCommands.CopyPath(vm.ItemPath);
                      });

                return _CopyPathCommand;
            }
        }

        /// <summary>
        /// Gets a command that executes when the selected item in the treeview has changed.
        /// This updates a text property to inform other attached dependencies property controls
        /// about this change in selection state.
        /// </summary>
        public ICommand SelectedFolderChangedCommand
        {
            get
            {
                if (_SelectedFolderChangedCommand == null)
                {
                    _SelectedFolderChangedCommand = new RelayCommand<object>((p) =>
                    {
                        var param = p as ITreeItemViewModel;

                        if (param != null)
                        {
                            SelectedItem = param;

                            try
                            {
                                var location = PathFactory.Create(param.ItemPath);
                                if (BrowseEvent != null)
                                    BrowseEvent(this, new BrowsingEventArgs(location, false, BrowseResult.Complete));
                            }
                            catch
                            {
                            }

                        }
                    });
                }

                return _SelectedFolderChangedCommand;
            }
        }

        /// <summary>
        /// Gets a command that executes when a user expands a tree view item node in the treeview.
        /// </summary>
        public ICommand ExpandCommand
        {
            get
            {
                if (_ExpandCommand == null)
                {
                    _ExpandCommand = new RelayCommand<object>((p) =>
                    {
                        if (IsBrowsing == true) // This is probably not relevant since the
                        {                      // viewmodel is currently processing a navigation
                            return;           // request to browse the view to a new location...
                        }

                        var expandedItem = p as ITreeItemViewModel;

                        if (expandedItem != null && _IsExpanding == false)
                        {
                            if (expandedItem.HasDummyChild == true)
                                ExpandDummyFolder(expandedItem);
                        }
                    });
                }

                return _ExpandCommand;
            }
        }

        /// <summary>
        /// Starts the rename folder process on the CommandParameter
        /// which must be FolderViewModel item that represented the to be renamed folder.
        /// 
        /// This command implements an event that triggers the actual rename
        /// process in the connected view.
        /// </summary>
        public ICommand StartRenameCommand
        {
            get
            {
                if (this._StartRenameCommand == null)
                    this._StartRenameCommand = new RelayCommand<object>(it =>
                    {
                        var folder = it as FolderViewModel;

                        if (folder != null)
                        {
                            folder.RequestEditMode(InplaceEditBoxLib.Events.RequestEditEvent.StartEditMode);
                        }
                    },
                    (it) =>
                    {
                        var folder = it as FolderViewModel;

                        if (folder != null)
                        {
                            if (folder.IsReadOnly == true)
                                return false;
                        }

                        return true;
                    });

                return this._StartRenameCommand;
            }
        }

        /// <summary>
        /// Renames the folder that is represented by this viewmodel.
        /// This command should be called directly by the implementing view
        /// since the new name of the folder is delivered in the
        /// CommandParameter as a string.
        /// </summary>
        public ICommand RenameCommand
        {
            get
            {
                if (this._RenameCommand == null)
                    this._RenameCommand = new RelayCommand<object>(it =>
                    {
                        var tuple = it as Tuple<string, object>;

                        if (tuple != null)
                        {
                            var folderVM = tuple.Item2 as FolderViewModel;

                            var newFolderName = tuple.Item1;

                            if (folderVM == null)
                                return;

                            if (string.IsNullOrEmpty(newFolderName) == false &&
                                folderVM != null)
                            {
                                var parent = folderVM.Parent;
                                if (parent != null)
                                {
                                    parent.ChildRename(folderVM.ItemName, newFolderName);

                                    this.SelectedFolder = folderVM.ItemPath;
                                }
                            }
                        }
                    });

                return this._RenameCommand;
            }
        }

        /// <summary>
        /// Starts the create folder process by creating a new folder
        /// in the given location. The location is supplied as <seealso cref="System.Windows.Input.ICommandSource.CommandParameter"/>
        /// which is a <seealso cref="ITreeItemViewModel"/> item.
        /// 
        /// So, the <seealso cref="ITreeItemViewModel"/> item is the parent of the new folder
        /// <seealso cref="IFolderViewModel"/> and the new folder is created with a standard
        /// name: 'New Folder n'. The new folder n is selected and in rename mode such that
        /// users can edit the name of the new folder right away.
        /// 
        /// This command implements an event that triggers the actual rename process in the
        /// connected view.
        /// </summary>
        public ICommand CreateFolderCommand
        {
            get
            {
                if (this._CreateFolderCommand == null)
                    this._CreateFolderCommand = new RelayCommand<object>(async it =>
                    {
                        var folder = it as TreeItemViewModel;

                        if (folder == null)
                            return;

                        if (folder.IsExpanded == false)
                        {
                            folder.IsExpanded = true;

                            // Refresh child items if this has been expanded for the 1st time
                            if (folder.HasDummyChild == true)
                            {
                                var x = await RequeryChildItems(folder);
                            }
                        }

                        this.CreateFolderCommandNewFolder(folder);
                    });

                return this._CreateFolderCommand;
            }
        }

        /// <summary>
        /// Gets command to select the current folder.
        /// 
        /// This binding can be used for browsing to a certain folder
        /// e.g. Users Document folder.
        /// 
        /// Expected parameter: string containing a path to be browsed to.
        /// </summary>
        [System.Obsolete("This is no longer supported.")]
        public ICommand FolderSelectedCommand
        {
            get
            {
                if (this._FolderSelectedCommand == null)
                {
                    this._FolderSelectedCommand = new RelayCommand<object>(p =>
                    {
                        string path = p as string;

                        if (string.IsNullOrEmpty(path) == true)
                            return;

                        if (IsBrowsing == true)
                            return;

                        BrowsePath(path, false);
                    },
                    (p) => { return ! IsBrowsing; });
                }

                return this._FolderSelectedCommand;
            }
        }

        /// <summary>
        /// Gets a command that will reload the folder view up to the
        /// selected path that is expected as <seealso cref="ITreeItemViewModel"/>
        /// in the CommandParameter.
        /// 
        /// This command is particularly useful when users create/delete a folder
        /// and want to update the treeview accordingly.
        /// </summary>
        public ICommand RefreshViewCommand
        {
            get
            {
                if (this._RefreshViewCommand == null)
                {
                    this._RefreshViewCommand = new RelayCommand<object>(p =>
                    {
                        try
                        {
                            var item = p as ITreeItemViewModel;

                            if (item == null)
                                return;

                            if (string.IsNullOrEmpty(item.ItemPath) == true)
                                return;

                            if (IsBrowsing == true)
                                return;

                            BrowsePath(item.ItemPath);
                        }
                        catch
                        {
                        }
                    }, (p) =>
                        {
                            return ! IsBrowsing;
                        });
                }

                return this._RefreshViewCommand;
            }
        }

        /// <summary>
        /// Expand folder for the very first time (using the process background viewmodel).
        /// </summary>
        /// <param name="expandedItem"></param>
        private void ExpandDummyFolder(ITreeItemViewModel expandedItem)
        {
            if (expandedItem != null && _IsExpanding == false)
            {
                if (expandedItem.HasDummyChild == true)
                {
                    _IsExpanding = true;

                    _ExpandProcessor.StartProcess(() =>
                    {
                        expandedItem.ClearFolders();                    // Requery sub-folders of this item
                        (expandedItem as TreeItemViewModel).LoadFolders();

                        ////expandedItem.IsSelected = true;
                        ////SelectedFolder = expandedItem.FolderPath;

                    }, ExpandProcessinishedEvent, "This process is already running.");
                }
            }
        }

        /// <summary>
        /// Methid executes when expand method is finished processing.
        /// </summary>
        /// <param name="processWasSuccessful"></param>
        /// <param name="exp"></param>
        /// <param name="caption"></param>
        private void ExpandProcessinishedEvent(bool processWasSuccessful, Exception exp, string caption)
        {
            _IsExpanding = false;
        }

        /// <summary>
        /// Requery all child items - this can be useful when we
        /// expand a folder for the very first time. Here we use task library with
        /// async to enable synchronization. This is for parts of other commands
        /// such as New Folder command which requires expansion of sub-folder
        /// items before actual New Folder command can execute.
        /// </summary>
        /// <param name="expandedItem"></param>
        /// <returns></returns>
        private async Task<bool> RequeryChildItems(TreeItemViewModel expandedItem)
        {
            await Task.Run(() => 
            {
                expandedItem.ClearFolders();  // Requery sub-folders of this item
                expandedItem.LoadFolders();
            });

            return true;
        }

        /// <summary>
        /// Gets a property to an object that is used to pop-up UI messages when errors occur.
        /// </summary>
        public IDisplayMessageViewModel DisplayMessage { get; private set; }

        /// <summary>
        /// Expose properties to commands that work with the bookmarking of folders.
        /// </summary>
        public IEditBookmarks BookmarkFolder { get; private set; }

        #region SpecialFolders property
        /// <summary>
        /// Gets a list of Special Windows Standard folders for display in view.
        /// </summary>
        public IEnumerable<ICustomFolderItemViewModel> SpecialFolders
        {
            get
            {
                return _SpecialFolders;
            }
        }

        /// <summary>
        /// Gets whether the browser view should show a special folder control or not
        /// (A special folder control lets users browse to folders like 'My Documents'
        /// with a mouse click).
        /// </summary>
        public bool IsSpecialFoldersVisisble
        {
            get
            {
                return _IsSpecialFoldersVisisble;
            }

            private set
            {
                if (_IsSpecialFoldersVisisble != value)
                {
                    _IsSpecialFoldersVisisble = value;
                    RaisePropertyChanged(() => IsSpecialFoldersVisisble);
                }
            }
        }
        #endregion SpecialFolders property

        /// <summary>
        /// Get/set property to indicate the initial path when control
        /// starts up via Loading. The control attempts to change the
        /// current directory to the indicated directory if the
        /// ... method is called in the Loaded event of the
        /// <seealso cref="FolderBrowserDialog"/>.
        /// </summary>
        public string InitialPath
        {
            get
            {
                return _InitalPath;

            }

            set
            {
                if (_InitalPath != value)
                {
                    _InitalPath = value;
                    RaisePropertyChanged(() => InitialPath);
                }
            }
        }
        #endregion properties

        #region methods
        /// <summary>
        /// Controller can start browser process if IsBrowsing = false
        /// </summary>
        /// <param name="newPath"></param>
        /// <returns></returns>
        public bool NavigateTo(IPathModel location)
        {
            CancellationTokenSource cts = null;
            bool BrowseMessage = false;
            try
            {
                IsBrowsing = true;

                if (BrowseMessage == true)
                {
                    if (BrowseEvent != null)   // Tell subscribers that we started browsing this directory
                        BrowseEvent(this, new BrowsingEventArgs(location, true));
                }

                IsBrowseViewEnabled = UpdateView = false;

                SetInitialDrives(cts);

////                        if (cts != null)
////                            cts.Token.ThrowIfCancellationRequested();
////
                InternalBrowsePath(location.Path, true, cts);

                return true;
            }
            finally
            {
                // Make sure that view updates at the end of browsing process
                IsBrowsing = false;
                IsBrowseViewEnabled = UpdateView = true;

                if (BrowseMessage == true)
                {
                    if (BrowseEvent != null)   // Tell subscribers that we started browsing this directory
                        BrowseEvent(this, new BrowsingEventArgs(location, false, BrowseResult.Complete));
                }
            }
        }

        /// <summary>
        /// Controller can start browser process if IsBrowsing = false
        /// </summary>
        /// <param name="newPath"></param>
        /// <returns></returns>
        async Task<bool> INavigateable.NavigateToAsync(IPathModel location)
        {
            return await Task.Run(() =>
            {
                return NavigateTo(location);
            });
        }

        /// <summary>
        /// Sets the IsExternalBrowsing state and cleans up any running processings
        /// if any. This method should only be called by an external controll instance.
        /// </summary>
        /// <param name="isBrowsing"></param>
        public void SetExternalBrowsingState(bool isBrowsing)
        {
            IsExternallyBrowsing = isBrowsing;
        }

        /// <summary>
        /// Determines whether the list of Windows special folder shortcut
        /// buttons (Music, Video etc) is visible or not.
        /// </summary>
        /// <param name="visible"></param>
        public void SetSpecialFoldersVisibility(bool visible)
        {
            this.IsSpecialFoldersVisisble = visible;
        }

        /// <summary>
        /// Call this method from the OnLoad method of the view
        /// in order to initialize a location as soon as the view
        /// is visible.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="ResetBrowserStatus"></param>
        public void BrowsePath(string path,
                               bool ResetBrowserStatus = true)
        {
            IPathModel location = null;
            try
            {
                location = PathFactory.Create(path);
            }
            catch
            {
                return;
            }

            _Processor.StartCancelableProcess(cts =>
            {
                try
                {
                    IsBrowsing = true;

                    if (BrowseEvent != null)   // Tell subscribers that we started browsing this directory
                        BrowseEvent(this, new BrowsingEventArgs(location, true));

                    IsBrowseViewEnabled = UpdateView = false;
                    
                    ClearFoldersCollections();
                    SetInitialDrives(cts);

                    if (cts != null)
                        cts.Token.ThrowIfCancellationRequested();

                    InternalBrowsePath(path, ResetBrowserStatus, cts);
                }
                finally
                {
                    // Make sure that view updates at the end of browsing process
                    IsBrowsing = false;
                    IsBrowseViewEnabled = UpdateView = true;
                }

            }, ProcessFinishedEvent, "This process is already running.");
        }

        private bool InternalBrowsePath(string path,
                                        bool ResetBrowserStatus,
                                        CancellationTokenSource cts = null)
        {
            if (ResetBrowserStatus == true)
                ClearBrowserStates();

            DisplayMessage.IsErrorMessageAvailable = false;

            if (System.IO.Directory.Exists(path) == false)
            {
                DisplayMessage.IsErrorMessageAvailable = true;
                DisplayMessage.Message = string.Format(FileSystemModels.Local.Strings.STR_ERROR_FOLDER_DOES_NOT_EXIST, path);
                return false;
            }

            SelectDirectory(PathFactory.Create(path, FSItemType.Folder), cts);

            return true;
        }

        private void AddFolder(string name, ITreeItemViewModel folder)
        {
           Application.Current.Dispatcher.Invoke(() =>
           {
                _Root.AddItem(folder);
            });
        }

        private void InitializeSpecialFolders()
        {
            _SpecialFolders = new ObservableCollection<ICustomFolderItemViewModel>();

            _SpecialFolders.Add(new CustomFolderItemViewModel(Environment.SpecialFolder.Desktop));
            _SpecialFolders.Add(new CustomFolderItemViewModel(Environment.SpecialFolder.MyDocuments));
            _SpecialFolders.Add(new CustomFolderItemViewModel(Environment.SpecialFolder.MyMusic));
            _SpecialFolders.Add(new CustomFolderItemViewModel(Environment.SpecialFolder.MyPictures));
            _SpecialFolders.Add(new CustomFolderItemViewModel(Environment.SpecialFolder.MyVideos));
        }

        private FolderViewModel CreateFolderItem(IPathModel model,
                                                 ITreeItemViewModel parent)
        {
            var f = new FolderViewModel(model, parent);

            return f;
        }

        /// <summary>
        /// Initialize the treeview with a set of local drives
        /// currently available on the computer.
        /// </summary>
        private void SetInitialDrives(CancellationTokenSource cts = null)
        {
            ClearFoldersCollections();

            var items = DriveModel.GetLogicalDrives().ToList();

            foreach (var item in items)
            {
                if (cts != null)
                    cts.Token.ThrowIfCancellationRequested();

                var vmItem = new DriveViewModel(item.Model, null);

                Application.Current.Dispatcher.Invoke(() =>
                {
                    _Root.AddItem(vmItem);
                });
            }
        }

        private void ClearFoldersCollections()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                _Root.Clear();
            });
        }

        private void SelectDirectory(IPathModel path,
                                     CancellationTokenSource cts = null)
        {
            string[] dirs = PathFactory.GetDirectories(path.Path);
            List<ITreeItemViewModel> PathItems = new List<ITreeItemViewModel>();

            if (dirs == null)
                return;

            ITreeItemViewModel root = _Root.TryGet(dirs[0]);

            // Find drive in which we will have to insert this
            if (root == null)
            {
                // Looks like this is a new drive - lets create it then ...
                root = new DriveViewModel(PathFactory.Create(dirs[0], FSItemType.LogicalDrive), null);

                AddFolder(root.ItemName, root);
            }

            PathItems.Add(root);
            var folderItem = MergeFolders(root, dirs, dirs[0], PathItems);

            for (int i = 0; i < PathItems.Count; i++)
            {
                if (cts != null)
                    cts.Token.ThrowIfCancellationRequested();

                var folder1 = (PathItems[i] as TreeItemViewModel);

                if (folder1.HasDummyChild == false)
                    folder1.IsExpanded = true;
                else
                {
                    folder1.LoadFolders();

                    if (folder1.HasDummyChild == false)
                        folder1.IsExpanded = true;
                    else
                        folder1.ClearFolders();

                }
            }

            ////PathItems[PathItems.Count - 1].IsExpanded = true;
            var folder = PathItems[PathItems.Count - 1] as TreeItemViewModel;
            SelectedItem = folder;
            folder.IsSelected = true;
        }

        private ITreeItemViewModel MergeFolders(ITreeItemViewModel root,
                                            string[] dirs,
                                            string accumulatedPath,
                                            List<ITreeItemViewModel> PathItems)
        {
            ITreeItemViewModel nextRoot = null;

            int i = 1;
            for (; i < dirs.Length; i++)
            {
                accumulatedPath = accumulatedPath + "/" + dirs[i];

                nextRoot = root.ChildTryGet(dirs[i]);

                if (nextRoot == null)
                {
                    (root as TreeItemViewModel).LoadFolders();     // Refresh children of this node

                    nextRoot = root.ChildTryGet(dirs[i]);

                    // Find Folder in which we will have to insert this
                    if (nextRoot == null)
                        return root;
                }

                PathItems.Add(nextRoot);
                root = nextRoot;
            }

            return root;
        }

        /// <summary>
        /// Create a new folder underneath the given parent folder. This method creates
        /// the folder with a standard name (eg 'New folder n') on disk and selects it
        /// in editing mode to give users a chance for renaming it right away.
        /// </summary>
        /// <param name="parentFolder"></param>
        private void CreateFolderCommandNewFolder(TreeItemViewModel parentFolder)
        {
            if (parentFolder == null)
                return;

            // Cast this to access internal methods and setters
            var item = parentFolder.CreateNewDirectory();
            var newSubFolder = item as FolderViewModel;
            SelectedItem = newSubFolder;

            ////this.SelectedFolder = newSubFolder.FolderPath;
            ////this.SetSelectedFolder(newSubFolder.FolderPath, true);

            if (newSubFolder != null)
            {
                // Do this with low priority (thanks for that tip to Joseph Leung)
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, (Action)delegate
                {
                    newSubFolder.IsSelected = true;
                    newSubFolder.RequestEditMode(InplaceEditBoxLib.Events.RequestEditEvent.StartEditMode);
                });
            }
        }

        /// <summary>
        /// Clear states of browser control (hide error message and other things that may not apply now)
        /// </summary>
        private void ClearBrowserStates()
        {
            DisplayMessage.Message = string.Empty;
            DisplayMessage.IsErrorMessageAvailable = false;
        }

        private void ProcessFinishedEvent(bool processWasSuccessful, Exception exp, string caption)
        {
            IPathModel location = null;
            try
            {
                location = PathFactory.Create(SelectedFolder);

                // Tell subscribers that we finished browsing this directory
                if (BrowseEvent != null)   // Tell subscribers that we started browsing this directory
                    BrowseEvent(this, new BrowsingEventArgs(location, false, BrowseResult.Complete));
            }
            catch
            {
                if (BrowseEvent != null)   // Tell subscribers that we started browsing this directory
                    BrowseEvent(this, new BrowsingEventArgs(location, false, BrowseResult.InComplete));
            }
        }
        #endregion methods
    }
}
