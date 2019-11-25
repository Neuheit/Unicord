﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Unicord.Universal.Models;
using Unicord.Universal.Utilities;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.ApplicationModel.Resources;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace Unicord.Universal.Pages.Settings
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ThemesSettingsPage : Page
    {
        private bool _changedTheme;
        private string _initialTheme;
        private int _initialColour;
        private bool _loaded;
        private bool _dragging;
        private bool _isDirty;

        public ThemesSettingsModel Model { get; }

        public ThemesSettingsPage()
        {
            Model = new ThemesSettingsModel();
            Model.PropertyChanged += ThemesSettingsPage_PropertyChanged;

            InitializeComponent();
            DataContext = Model;

            _initialTheme = App.LocalSettings.Read("SelectedThemeName", string.Empty);
            if (string.IsNullOrWhiteSpace(_initialTheme))
                _initialTheme = "Default";

            _initialColour = (int)App.LocalSettings.Read("RequestedTheme", ElementTheme.Default);
        }

        private async void ThemesSettingsPage_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is ThemesSettingsModel model)
            {
                //var theme = model.SelectedTheme as Theme;
                //if (model.ColourScheme != _initialColour || theme?.Name != _initialTheme)
                //{
                //    _changedTheme = true;
                //    relaunchRequired.Visibility = Visibility.Visible;
                //}
                //else
                //{
                //    _changedTheme = false;
                //    relaunchRequired.Visibility = Visibility.Collapsed;
                //}

                //var dictionary = new ResourceDictionary();
                //if (!string.IsNullOrWhiteSpace(theme?.Name) && !theme.IsDefault)
                //{
                //    try
                //    {
                //        await ThemeManager.LoadAsync(theme.Name, dictionary);
                //    }
                //    catch (Exception ex)
                //    {
                //        await UIUtilities.ShowErrorDialogAsync("Failed to load theme!", ex.Message);
                //    }
                //}

                // if we invert the theme then set it properly, the element will redraw and reload
                // it's resources. as far as i know there's no better way to do this.

                switch ((ElementTheme)model.ColourScheme)
                {
                    case ElementTheme.Light:
                        preview.RequestedTheme = ElementTheme.Dark;
                        break;
                    case ElementTheme.Dark:
                        preview.RequestedTheme = ElementTheme.Light;
                        break;
                    default:
                        preview.RequestedTheme = Application.Current.RequestedTheme == ApplicationTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
                        break;
                }

                //Resources = dictionary;
                RequestedTheme = (ElementTheme)model.ColourScheme;
            }
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            App.LocalSettings.Save("SelectedThemeNames", Model.SelectedThemes.OrderBy(t => Model.AvailableThemes.IndexOf(t)).Select(s => s.NormalisedName).ToList());

            var resources = ResourceLoader.GetForCurrentView("ThemesSettingsPage");
            var autoRestart = ApiInformation.IsMethodPresent("Windows.ApplicationModel.Core.CoreApplication", "RequestRestartAsync");
            if (_changedTheme && autoRestart)
            {
                if (await UIUtilities.ShowYesNoDialogAsync(resources.GetString("ThemeChangedTitle"), resources.GetString("ThemeChangedMessage")))
                {
                    await CoreApplication.RequestRestartAsync("");
                }
            }
        }
        
        private async void RemoveThemeButton_Click(object sender, RoutedEventArgs e)
        {
            //if (Model.SelectedTheme is Theme theme)
            //{
            //    await ThemeManager.RemoveThemeAsync(theme.Name);
            //    await Model.ReloadThemes();
            //}
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            var args = this.FindParent<MainPage>()?.Arguments;
            var resources = ResourceLoader.GetForCurrentView("ThemesSettingsPage");
            if (args != null && args.ThemeLoadException != null)
            {
                themeLoadError.Visibility = Visibility.Visible;
                themeLoadError.Text = string.Format(resources.GetString("ThemeLoadFailedFormat"), args.ThemeLoadException.Message);
            }

            await Model.ReloadThemes();
            FixSelectedThemes();
        }

        private void FixSelectedThemes()
        {
            _loaded = false;

            themesList.SelectedItems.Clear();
            foreach (var item in Model.SelectedThemes)
            {
                themesList.SelectedItems.Add(item);
            }

            _loaded = true;
        }

        private async void AddThemesButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".uni-theme");
            picker.FileTypeFilter.Add(".zip");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                try
                {
                    var theme = await ThemeManager.InstallFromFileAsync(file);
                }
                catch (Exception ex)
                {
                    await UIUtilities.ShowErrorDialogAsync("Failed to install theme!", ex.Message);
                }
            }

            await Model.ReloadThemes();
        }

        private void RemoveThemesButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void ThemesList_DragOver(object sender, DragEventArgs e)
        {
            var deferral = e.GetDeferral();

            if (e.DataView.AvailableFormats.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Any(i => { var ext = Path.GetExtension(i.Name); return ext == ".zip" || ext == ".uni-theme"; }))
                {
                    e.Handled = true;
                    e.AcceptedOperation = DataPackageOperation.Copy;
                }
            }

            deferral.Complete();
        }

        private async void ThemesList_Drop(object sender, DragEventArgs e)
        {
            var deferral = e.GetDeferral();

            if (e.DataView.AvailableFormats.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                var themeItems = items.OfType<StorageFile>().Where(i => { var ext = Path.GetExtension(i.Name); return ext == ".zip" || ext == ".uni-theme"; });
                foreach (var theme in themeItems)
                {
                    e.Handled = true;
                    e.AcceptedOperation = DataPackageOperation.Copy;

                    deferral?.Complete();
                    deferral = null;

                    await ThemeManager.InstallFromFileAsync(theme);
                }
            }
        }

        private void ThemesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Model.IsLoading || !_loaded || _dragging)
                return;

            foreach (var item in e.AddedItems.OfType<Theme>())
            {
                Model.SelectedThemes.Add(item);
            }

            foreach (var item in e.RemovedItems.OfType<Theme>())
            {
                Model.SelectedThemes.Remove(item);
            }

            var names = Model.SelectedThemes.OrderBy(t => Model.AvailableThemes.IndexOf(t)).Select(s => s.NormalisedName).ToList();
            App.LocalSettings.Save("SelectedThemeNames", names);

            Model.IsDirty = true;
            ReloadThemes(names, preview);
        }

        private void ReloadThemes(List<string> names, FrameworkElement el)
        {
            var dictionary = new ResourceDictionary();
            ThemeManager.Load(names, dictionary);

            switch ((ElementTheme)Model.ColourScheme)
            {
                case ElementTheme.Light:
                    preview.RequestedTheme = ElementTheme.Dark;
                    break;
                case ElementTheme.Dark:
                    preview.RequestedTheme = ElementTheme.Light;
                    break;
                default:
                    preview.RequestedTheme = Application.Current.RequestedTheme == ApplicationTheme.Light ? ElementTheme.Dark : ElementTheme.Light;
                    break;
            }

            el.Resources = dictionary;
            el.RequestedTheme = (ElementTheme)Model.ColourScheme;
            el.InvalidateMeasure();
            el.InvalidateArrange();
        }

        private void ThemesList_DragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            _dragging = true;
        }

        private void ThemesList_DragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            _dragging = false;
        }
    }
}
