﻿namespace FileListView
{
    using FileListView.Interfaces;
    using FileListView.ViewModels;
    using FileListView.ViewModels.Interfaces;
    using FileSystemModels;
    using FileSystemModels.Interfaces;
    using FileSystemModels.Models.FSItems.Base;

    public sealed class Factory
    {
        private Factory(){ }

        public static IFileListViewModel CreateFileListViewModel(IBrowseNavigation browseNavigation)
        {
            return new FileListViewModel(browseNavigation);
        }

        public static IFilterComboBoxViewModel CreateFilterComboBoxViewModel()
        {
            return new FilterComboBoxViewModel();
        }

        public static IFilterItemViewModel CreateFilterItem()
        {
            return new FilterItemViewModel();
        }

        public static ILVItemViewModel CreateItem(
              string path
            , FSItemType type
            , string displayName)
        {
            return new LVItemViewModel(path, type, displayName);
        }

        /// <summary>
        /// Public construction method to create a <see cref="ILVItemViewModel"/>
        /// object that represents a logical drive (eg 'C:\')
        /// </summary>
        /// <param name="curdir"></param>
        /// <returns></returns>
        public static ILVItemViewModel CreateLogicalDrive(string curdir)
        {
            var item = new LVItemViewModel(
                PathFactory.Create(curdir, FSItemType.LogicalDrive),
                string.Empty, true);

            item.SetDisplayName(item.DisplayItemString());

            return item;
        }

        public static IFolderComboBoxViewModel CreateFolderComboBoxVM()
        {
            return new FolderComboBoxViewModel();
        }
    }
}