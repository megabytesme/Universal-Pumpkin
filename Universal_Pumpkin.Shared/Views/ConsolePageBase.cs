using System;
using System.Collections.Generic;
using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Universal_Pumpkin.Helpers;
using Universal_Pumpkin.Models;
using Universal_Pumpkin.ViewModels;
using Windows.UI.Xaml.Navigation;
using System.Diagnostics;
using System.Collections.ObjectModel;

namespace Universal_Pumpkin.Shared.Views
{
    public abstract class ConsolePageBase : Page
    {
        protected readonly ConsoleViewModel _vm;
        protected readonly ObservableCollection<LogEntry> _visibleLogItems = new ObservableCollection<LogEntry>();

        protected bool _autoScroll = true;
        protected bool _ignoreNextViewChanged = false;

        protected readonly HashSet<string> _enabledFilters =
            new HashSet<string> { "INFO", "WARN", "ERROR", "DEBUG" };

        protected ListView _cachedLogList;
        protected TextBox _cachedSearchBox;
        protected Button _cachedResumeButton;
        protected TextBlock _cachedTxtRam, _cachedTxtTps, _cachedTxtMspt, _cachedTxtIp;
        protected Grid _cachedStatusGrid;
        protected Button _cachedBtnStart, _cachedBtnStop, _cachedBtnRestart;
        protected AutoSuggestBox _cachedBoxCommand;
        protected Button _cachedBtnSend;
        protected ProgressRing _cachedLoadingSpinner;

        protected ConsolePageBase()
        {
            _vm = new ConsoleViewModel();

            _vm.LogReceived += Vm_LogReceived;
            _vm.ServerStopped += Vm_ServerStopped;
            _vm.MetricsUpdated += Vm_MetricsUpdated;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        // Navigation
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            _cachedLogList = this.FindName("LogList") as ListView;
            _cachedSearchBox = this.FindName("SearchBox") as TextBox;
            _cachedResumeButton = this.FindName("ResumeAutoScrollButton") as Button;
            _cachedTxtRam = this.FindName("TxtRAM") as TextBlock;
            _cachedTxtTps = this.FindName("TxtTPS") as TextBlock;
            _cachedTxtMspt = this.FindName("TxtMSPT") as TextBlock;
            _cachedTxtIp = this.FindName("TxtIpAddress") as TextBlock;
            _cachedStatusGrid = this.FindName("StatusGrid") as Grid;
            _cachedBtnStart = this.FindName("BtnStart") as Button;
            _cachedBtnStop = this.FindName("BtnStop") as Button;
            _cachedBtnRestart = this.FindName("BtnRestartApp") as Button;
            _cachedBoxCommand = this.FindName("BoxCommand") as AutoSuggestBox;
            _cachedBtnSend = this.FindName("BtnSend") as Button;
            _cachedLoadingSpinner = this.FindName("LoadingSpinner") as ProgressRing;

            _vm.OnNavigatedTo();
            UpdateInfoBarForNotRunning();
            UpdateUiState();
            
            if (LogList != null)
            {
                LogList.ItemsSource = _visibleLogItems;
                LogList.Loaded += LogList_Loaded;
                TryHookScrollEvent();
            }

            if (_cachedLoadingSpinner != null) _cachedLoadingSpinner.Visibility = Visibility.Visible;
            await _vm.LoadInitialLogAsync();
            if (_cachedLoadingSpinner != null) _cachedLoadingSpinner.Visibility = Visibility.Collapsed;

            ApplyFilter();

            if (_autoScroll && _visibleLogItems.Count > 0)
            {
                LogList.ScrollIntoView(_visibleLogItems[_visibleLogItems.Count - 1]);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.OnNavigatedFrom();
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
        }

        // VM Event Handlers
        private async void Vm_MetricsUpdated(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_cachedTxtRam != null) _cachedTxtRam.Text = _vm.RamUsage;
                if (_cachedTxtTps != null) _cachedTxtTps.Text = _vm.TpsText;
                if (_cachedTxtMspt != null) _cachedTxtMspt.Text = _vm.MsptText;

                if (_cachedTxtTps != null)
                {
                    _cachedTxtTps.Foreground = _vm.IsTpsGood
                        ? new SolidColorBrush(Windows.UI.Colors.LightGreen)
                        : new SolidColorBrush(Windows.UI.Colors.OrangeRed);
                }
            });
        }

        private async void Vm_LogReceived(object sender, LogEntry entry)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
            {
                _vm.LogItems.Add(entry);

                bool passesFilter = _enabledFilters.Contains(entry.Level);

                if (passesFilter && SearchBox != null && !string.IsNullOrEmpty(SearchBox.Text))
                {
                    var query = SearchBox.Text.Trim();
                    passesFilter = entry.Message.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                   entry.Level.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                }
                
                if (passesFilter)
                {
                    _visibleLogItems.Add(entry);

                    if (_autoScroll && _visibleLogItems.Count > 0)
                    {
                        LogList.ScrollIntoView(_visibleLogItems[_visibleLogItems.Count - 1]);
                    }
                }
            });
        }

        private async void Vm_ServerStopped(object sender, int code)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StatusGrid.Visibility = Visibility.Collapsed;
                BtnStop.Visibility = Visibility.Collapsed;
                BtnStart.Visibility = Visibility.Collapsed;
                BtnRestartApp.Visibility = Visibility.Visible;

                BoxCommand.IsEnabled = false;
                BtnSend.IsEnabled = false;

                OnServerStoppedUI(code);

                _vm.LogItems.Add(new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Message = "[System] Server stopped. Please restart the app to run again."
                });

                ApplyFilter();

                if (LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
            });
        }

        // Abstract UI Hooks
        protected abstract void UpdateUiState();
        protected abstract void UpdateInfoBarForNotRunning();
        protected abstract void OnServerStoppedUI(int code);
        protected abstract void OnServerStoppingUI();
        protected abstract void OnStartServerError();
        protected abstract void ShowRestartDialog();

        // Server Control
        protected async void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vm.StartServerAsync();
                UpdateUiState();

                _vm.LogItems.Clear();
                ApplyFilter();

                TxtIpAddress.Text = _vm.LocalIpAddress;
                StatusGrid.Visibility = Visibility.Visible;
            }
            catch (Exception)
            {
                OnStartServerError();
            }
        }

        protected void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            BtnStop.IsEnabled = false;
            OnServerStoppingUI();
            _vm.StopServer();
        }

        protected void BtnRestartApp_Click(object sender, RoutedEventArgs e)
        {
            ShowRestartDialog();
        }

        // Server Command Input
        protected async void BoxCommand_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            var items = await _vm.GetSuggestionsAsync(sender.Text);
            sender.ItemsSource = items;
        }

        protected void BoxCommand_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string command = string.IsNullOrEmpty(args.QueryText) ? sender.Text : args.QueryText;
            _vm.SendCommand(command);
            BoxCommand.Text = "";
            BoxCommand.Focus(FocusState.Programmatic);
        }

        protected void BoxCommand_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            string selected = args.SelectedItem.ToString();
            string currentText = sender.Text;

            int lastSpace = currentText.LastIndexOf(' ');

            sender.Text = (lastSpace >= 0 ? currentText.Substring(0, lastSpace + 1) : "") + selected;

            var textBox = FindChild<TextBox>(sender);
            if (textBox != null) textBox.SelectionStart = sender.Text.Length;
        }

        protected static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T t) return t;
                var result = FindChild<T>(child);
                if (result != null) return result;
            }
            return null;
        }

        protected void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            _vm.SendCommand(BoxCommand.Text);
            BoxCommand.Text = "";
            BoxCommand.Focus(FocusState.Programmatic);
        }

        // Auto Scroll
        private void TryHookScrollEvent()
        {
            LogList.ApplyTemplate();

            var sv = LogList.GetFirstDescendantOfType<ScrollViewer>();
            if (sv != null)
            {
                sv.ViewChanged -= LogScrollViewer_ViewChanged;
                sv.ViewChanged += LogScrollViewer_ViewChanged;
            }
        }

        protected void LogList_Loaded(object sender, RoutedEventArgs e)
        {
            TryHookScrollEvent();
        }

        protected void LogScrollViewer_ViewChanged(object sender, ScrollViewerViewChangedEventArgs e)
        {
            if (_ignoreNextViewChanged)
            {
                _ignoreNextViewChanged = false;
                return;
            }

            var sv = (ScrollViewer)sender;

            bool isAtBottom = sv.VerticalOffset >= (sv.ScrollableHeight - 40);

            if (isAtBottom)
            {
                if (!_autoScroll)
                {
                    _autoScroll = true;
                    if (ResumeAutoScrollButton != null)
                        ResumeAutoScrollButton.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                if (!e.IsIntermediate && _autoScroll)
                {
                    _autoScroll = false;
                    if (ResumeAutoScrollButton != null)
                        ResumeAutoScrollButton.Visibility = Visibility.Visible;
                }
            }
        }

        protected void ResumeAutoScroll_Click(object sender, RoutedEventArgs e)
        {
            _ignoreNextViewChanged = true;
            _autoScroll = true;

            if (ResumeAutoScrollButton != null)
                ResumeAutoScrollButton.Visibility = Visibility.Collapsed;

            if (LogList.Items.Count > 0)
            {
                LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
            }
        }

        // Search + Filtering
        protected void Search_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Visibility == Visibility.Visible)
            {
                SearchBox.Visibility = Visibility.Collapsed;
                SearchBox.Text = "";
                ApplyFilter();
            }
            else
            {
                SearchBox.Visibility = Visibility.Visible;
                SearchBox.Focus(FocusState.Programmatic);
            }
        }

        protected void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var query = SearchBox.Text.Trim();

            if (string.IsNullOrEmpty(query))
            {
                ApplyFilter();
                return;
            }

            LogList.ItemsSource = _vm.LogItems
                .Where(x => x.Message.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                         || x.Level.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
                .Where(x => _enabledFilters.Contains(x.Level))
                .ToList();
        }

        protected void ApplyFilter()
        {
            if (LogList == null) return;

            var query = SearchBox?.Text?.Trim();

            var filtered = _vm.LogItems.Where(x => _enabledFilters.Contains(x.Level));

            if (!string.IsNullOrEmpty(query))
            {
                filtered = filtered.Where(x => x.Message.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                                           || x.Level.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            _visibleLogItems.Clear();
            foreach (var item in filtered)
            {
                _visibleLogItems.Add(item);
            }

            if (_autoScroll && _visibleLogItems.Count > 0)
            {
                LogList.ScrollIntoView(_visibleLogItems[_visibleLogItems.Count - 1]);
            }
        }

        // Selection (Context Menu)
        protected void SelectItemWithoutClearing(LogEntry entry)
        {
            if (!LogList.SelectedItems.Contains(entry))
                LogList.SelectedItems.Add(entry);
        }

        protected void LogItem_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is LogEntry entry)
            {
                SelectItemWithoutClearing(entry);
                ShowLogContextMenu(entry, fe, e.GetPosition(fe));
            }
        }

        protected void LogItem_Holding(object sender, HoldingRoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is LogEntry entry)
            {
                SelectItemWithoutClearing(entry);
                ShowLogContextMenu(entry, fe, e.GetPosition(fe));
            }
        }

        protected void LogBackground_RightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            if (fe != null)
                ShowBackgroundContextMenu(fe, e.GetPosition(fe));
        }

        protected void LogBackground_Holding(object sender, HoldingRoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            if (fe != null)
                ShowBackgroundContextMenu(fe, e.GetPosition(fe));
        }

        protected void CommandBarMenu_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            if (fe != null)
                ShowBackgroundContextMenu(fe, new Windows.Foundation.Point(0, fe.ActualHeight));
        }

        // Context Menus
        protected void ShowBackgroundContextMenu(FrameworkElement target, Windows.Foundation.Point position)
        {
            var menu = BuildContextMenu();
            menu.ShowAt(target, position);
        }

        protected void ShowLogContextMenu(LogEntry entry, FrameworkElement target, Windows.Foundation.Point position)
        {
            var menu = BuildContextMenu();
            menu.ShowAt(target, position);
        }

        private MenuFlyout BuildContextMenu()
        {
            var menu = new MenuFlyout();
            
            if (LogList.SelectedItems.Count >= 1)
            {
                var copyItem = new MenuFlyoutItem
                {
                    Text = "Copy"
                };
#if UWP1709
                copyItem.Icon = new SymbolIcon(Symbol.Copy);
#endif
                copyItem.Click += (s, e) => CopySelectedLogs();
                menu.Items.Add(copyItem);

                var deleteItem = new MenuFlyoutItem
                {
                    Text = "Delete"
                };
#if UWP1709
                deleteItem.Icon = new SymbolIcon(Symbol.Delete);
#endif
                deleteItem.Click += (s, e) => DeleteSelectedLogs();
                menu.Items.Add(deleteItem);

                menu.Items.Add(new MenuFlyoutSeparator());
            }
            
            var selectAllItem = new MenuFlyoutItem
            {
                Text = "Select All"
            };
#if UWP1709
            selectAllItem.Icon = new SymbolIcon(Symbol.SelectAll);
#endif
            selectAllItem.Click += (s, e) => SelectAllLogs();
            menu.Items.Add(selectAllItem);
            
            if (LogList.SelectedItems.Count >= 1)
            {
                var clearSelectionItem = new MenuFlyoutItem
                {
                    Text = "Clear Selection"
                };
#if UWP1709
                clearSelectionItem.Icon = new SymbolIcon(Symbol.Clear);
#endif
                clearSelectionItem.Click += (s, e) => LogList.SelectedItems.Clear();
                menu.Items.Add(clearSelectionItem);
            }

            menu.Items.Add(new MenuFlyoutSeparator());
            var filterSub = new MenuFlyoutSubItem
            {
                Text = "Filter"
            };
#if UWP1709
            filterSub.Icon = new SymbolIcon(Symbol.Filter);
#endif

            filterSub.Items.Add(CreateFilterItem("INFO"));
            filterSub.Items.Add(CreateFilterItem("WARN"));
            filterSub.Items.Add(CreateFilterItem("ERROR"));
            filterSub.Items.Add(CreateFilterItem("DEBUG"));

            menu.Items.Add(filterSub);

            return menu;
        }

        private void CopySelectedLogs()
        {
            var selected = LogList.SelectedItems.Cast<LogEntry>();
            if (!selected.Any()) return;

            var text = string.Join("\n", selected.Select(x =>
                $"{x.Timestamp:HH:mm:ss} [{x.Level}] {x.Message}"));

            var dp = new DataPackage();
            dp.SetText(text);
            Clipboard.SetContent(dp);
        }

        private void SelectAllLogs()
        {
            LogList.SelectedItems.Clear();
            foreach (var item in LogList.Items)
                LogList.SelectedItems.Add(item);
        }

        private void DeleteSelectedLogs()
        {
            var selected = LogList.SelectedItems.Cast<LogEntry>().ToList();
            foreach (var entry in selected)
                _vm.LogItems.Remove(entry);

            ApplyFilter();
        }

        private ToggleMenuFlyoutItem CreateFilterItem(string level)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = level,
                Tag = level,
                IsChecked = _enabledFilters.Contains(level)
            };
            item.Click += FilterMenu_Click;
            return item;
        }

        private void FilterMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string level)
            {
                if (item.IsChecked)
                    _enabledFilters.Add(level);
                else
                    _enabledFilters.Remove(level);

                ApplyFilter();
            }
        }

        // Keyboard Shortcuts
        protected void LogList_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == VirtualKey.C &&
                (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0)
            {
                CopySelectedLogs();
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.A &&
                (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0)
            {
                SelectAllLogs();
                e.Handled = true;
                return;
            }

            if (e.Key == VirtualKey.Delete)
            {
                DeleteSelectedLogs();
                e.Handled = true;
                return;
            }
        }

        protected void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (args.VirtualKey == VirtualKey.F &&
                (Window.Current.CoreWindow.GetKeyState(VirtualKey.Control) & CoreVirtualKeyStates.Down) != 0)
            {
                SearchBox.Visibility = Visibility.Visible;
                SearchBox.Focus(FocusState.Programmatic);
            }
        }

        // Shared UI Elements
        protected ListView LogList => _cachedLogList;
        protected TextBox SearchBox => _cachedSearchBox;
        protected Button ResumeAutoScrollButton => _cachedResumeButton;
        protected TextBlock TxtRAM => _cachedTxtRam;
        protected TextBlock TxtTPS => _cachedTxtTps;
        protected TextBlock TxtMSPT => _cachedTxtMspt;
        protected TextBlock TxtIpAddress => _cachedTxtIp;
        protected Grid StatusGrid => _cachedStatusGrid;
        protected Button BtnStart => _cachedBtnStart;
        protected Button BtnStop => _cachedBtnStop;
        protected Button BtnRestartApp => _cachedBtnRestart;
        protected AutoSuggestBox BoxCommand => _cachedBoxCommand;
        protected Button BtnSend => _cachedBtnSend;
        protected ProgressRing LoadingSpinner => _cachedLoadingSpinner;
    }
}