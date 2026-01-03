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
using Universal_Pumpkin.Services;
using System.Threading.Tasks;
using Windows.UI.Xaml.Documents;
using System.Collections.Concurrent;

namespace Universal_Pumpkin.Shared.Views
{
    public abstract class ConsolePageBase : Page
    {
        protected readonly ConsoleViewModel _vm;

        protected bool _autoScroll = true;
        protected bool _ignoreNextViewChanged = false;

        protected ListView _cachedLogList;
        protected TextBox _cachedSearchBox;
        protected Button _cachedResumeButton;
        protected TextBlock _cachedTxtRam, _cachedTxtTps, _cachedTxtMspt, _cachedTxtIp;
        protected Grid _cachedStatusGrid;
        protected Button _cachedBtnStart, _cachedBtnStop, _cachedBtnRestart;
        protected AutoSuggestBox _cachedBoxCommand;
        protected TextBlock _cachedGhostText;
        protected Button _cachedBtnSend;
        protected ProgressRing _cachedLoadingSpinner;
        private ScrollViewer _cachedScrollViewer;

        private List<string> _commandHistory = new List<string>();
        private int _historyIndex = -1;
        private string _currentPrediction = "";

        private bool isAtBottom;

        protected ConsolePageBase()
        {
            _vm = new ConsoleViewModel();

            InlineHelper.RunCommandCallback = (cmd) =>
            {
                _vm.SendCommand(cmd);
            };

            InlineHelper.SuggestCommandCallback = (cmd) =>
            {
                BoxCommand.Text = cmd;
                BoxCommand.Focus(FocusState.Programmatic);
            };

            _vm.LogReceived += Vm_LogReceived;
            _vm.ServerStopped += Vm_ServerStopped;
            _vm.MetricsUpdated += Vm_MetricsUpdated;

            Window.Current.CoreWindow.KeyDown += CoreWindow_KeyDown;
        }

        private static string NormalizeMcCommand(string cmd)
        {
            if (string.IsNullOrWhiteSpace(cmd))
                return cmd;

            if (cmd.StartsWith("/"))
                return cmd.Substring(1);

            return cmd;
        }

        protected void LogMessageTextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is TextBlock tb && tb.DataContext is LogEntry entry)
            {
                tb.Inlines.Clear();

                if (entry.Segments == null || entry.Segments.Count == 0)
                {
                    tb.Inlines.Add(new Run { Text = entry.Message });
                    return;
                }

                foreach (var seg in entry.Segments)
                {
                    Inline inline;
                    if (!string.IsNullOrEmpty(seg.HyperlinkTarget))
                    {
                        var link = new Hyperlink();
                        link.Inlines.Add(new Run { Text = seg.Text });
                        link.Click += LogHyperlink_Click;
                        HyperlinkExtensions.SetTarget(link, seg.HyperlinkTarget);
                        inline = link;
                    }
                    else
                    {
                        inline = new Run { Text = seg.Text };
                    }

                    ApplyAnsiStyleToInline(inline, seg.Style);
                    tb.Inlines.Add(inline);
                }
            }
        }

        private async void LogHyperlink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            var target = HyperlinkExtensions.GetTarget(sender);
            if (string.IsNullOrEmpty(target))
                return;

            try
            {
                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    target.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    await Launcher.LaunchUriAsync(new Uri(target));
                }
                else if (target.StartsWith("mc:copy_to_clipboard:", StringComparison.OrdinalIgnoreCase))
                {
                    var value = target.Substring("mc:copy_to_clipboard:".Length);
                    var dp = new DataPackage();
                    dp.SetText(value);
                    Clipboard.SetContent(dp);
                }
                else if (target.StartsWith("mc:run_command:", StringComparison.OrdinalIgnoreCase))
                {
                    var cmd = target.Substring("mc:run_command:".Length);
                    _vm.SendCommand(NormalizeMcCommand(cmd));
                }
                else if (target.StartsWith("mc:suggest_command:", StringComparison.OrdinalIgnoreCase))
                {
                    var cmd = target.Substring("mc:suggest_command:".Length);
                    BoxCommand.Text = NormalizeMcCommand(cmd);
                    BoxCommand.Focus(FocusState.Programmatic);
                }
                else if (target.StartsWith("mc:change_page:", StringComparison.OrdinalIgnoreCase))
                {
                    // No-op for now
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hyperlink click error: {ex.Message}");
            }
        }

        private void ApplyAnsiStyleToInline(Inline inline, AnsiStyle style)
        {
            if (inline == null || style == null) return;

            if (inline is Run run)
            {
                if (style.Bold)
                    run.FontWeight = Windows.UI.Text.FontWeights.Bold;
                if (style.Italic)
                    run.FontStyle = Windows.UI.Text.FontStyle.Italic;
#if UWP1709                
                if (style.Underline)
                    run.TextDecorations |= Windows.UI.Text.TextDecorations.Underline;
                if (style.Strikethrough)
                    run.TextDecorations |= Windows.UI.Text.TextDecorations.Strikethrough;
#endif
                if (style.Foreground.HasValue)
                    run.Foreground = new SolidColorBrush(style.Foreground.Value);
            }
            else if (inline is Hyperlink link)
            {
                if (style.Foreground.HasValue)
                    link.Foreground = new SolidColorBrush(style.Foreground.Value);
            }
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
            _cachedGhostText = this.FindName("GhostText") as TextBlock;
            _cachedBtnSend = this.FindName("BtnSend") as Button;
            _cachedLoadingSpinner = this.FindName("LoadingSpinner") as ProgressRing;

            _vm.OnNavigatedTo();
            UpdateInfoBarForNotRunning();
            UpdateUiState();

            if (LogList != null)
            {
                LogList.ItemsSource = _vm.VisibleLogs;
                LogList.Loaded += LogList_Loaded;
                TryHookScrollEvent();
            }

            if (_cachedLoadingSpinner != null) _cachedLoadingSpinner.Visibility = Visibility.Visible;
            await _vm.LoadInitialLogAsync();
            if (_cachedLoadingSpinner != null) _cachedLoadingSpinner.Visibility = Visibility.Collapsed;

            _vm.ApplyFilter();
            ScrollToBottomIfNeeded();

            if (_autoScroll && _vm.VisibleLogs.Count > 0)
            {
                LogList.ScrollIntoView(_vm.VisibleLogs[_vm.VisibleLogs.Count - 1]);
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            _vm.OnNavigatedFrom();
            Window.Current.CoreWindow.KeyDown -= CoreWindow_KeyDown;
        }

        // VM Event Handlers
        private void Vm_LogReceived(object sender, LogEntry entry)
        {
            _vm.EnqueueLog(entry);
            ScrollToBottomIfNeeded();
        }

        private async void Vm_MetricsUpdated(object sender, EventArgs e)
        {
            await RunOnUI(CoreDispatcherPriority.Normal, () =>
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

                var systemEntry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Message = "[System] Server stopped. Please restart the app to run again."
                };
                _vm.EnqueueLog(systemEntry);

                _vm.ApplyFilter();
                ScrollToBottomIfNeeded();

                if (LogList.Items.Count > 0)
                    LogList.ScrollIntoView(LogList.Items[LogList.Items.Count - 1]);
            });
        }

        protected async Task RunOnUI(CoreDispatcherPriority priority, Action action)
        {
            if (Dispatcher == null) return;

            if (Dispatcher.HasThreadAccess)
            {
                action();
            }
            else
            {
                try
                {
                    await Dispatcher.RunAsync(priority, () => action());
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Dispatcher error: {ex.Message}");
                }
            }
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

                _vm.AllLogs.Clear();
                _vm.VisibleLogs.Clear();
                _vm.ApplyFilter();
                ScrollToBottomIfNeeded();

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
        protected void BoxCommand_GotFocus(object sender, RoutedEventArgs e)
        {
            if (AppearanceService.Current != AppearanceMode.Win11 && _cachedGhostText != null)
            {
                _cachedGhostText.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                _cachedGhostText.Opacity = 0.6;
            }
        }

        protected void BoxCommand_LostFocus(object sender, RoutedEventArgs e)
        {
            if (AppearanceService.Current != AppearanceMode.Win11 && _cachedGhostText != null)
            {
                _cachedGhostText.Foreground =
                    (Brush)Application.Current.Resources["SystemControlForegroundBaseHighBrush"];
                _cachedGhostText.Opacity = 0.6;
            }
        }

        protected async void BoxCommand_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
            {
                if (args.Reason == AutoSuggestionBoxTextChangeReason.ProgrammaticChange)
                    _cachedGhostText.Text = "";
                return;
            }

            if (string.IsNullOrWhiteSpace(sender.Text))
            {
                sender.ItemsSource = null;
                _cachedGhostText.Text = "";
                _currentPrediction = "";
                return;
            }

            var suggestions = await _vm.GetSuggestionsAsync(sender.Text);
            sender.ItemsSource = suggestions;

            if (suggestions.Count > 0)
            {
                string bestMatch = suggestions[0];
                _currentPrediction = bestMatch;

                if (bestMatch.StartsWith(sender.Text, StringComparison.OrdinalIgnoreCase))
                {
                    _cachedGhostText.Text = bestMatch;
                }
                else
                {
                    _cachedGhostText.Text = "";
                }
            }
            else
            {
                _cachedGhostText.Text = "";
                _currentPrediction = "";
            }
        }

        protected void BoxCommand_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            var senderBox = sender as AutoSuggestBox;

            if (e.Key == VirtualKey.Tab && !string.IsNullOrEmpty(_currentPrediction))
            {
                senderBox.Text = _currentPrediction;
                _cachedGhostText.Text = "";
                e.Handled = true;

                var textBox = FindChild<TextBox>(senderBox);
                if (textBox != null) textBox.SelectionStart = senderBox.Text.Length;
                return;
            }

            if (e.Key == VirtualKey.Up)
            {
                if (_commandHistory.Count > 0)
                {
                    if (_historyIndex == -1) _historyIndex = _commandHistory.Count - 1;
                    else if (_historyIndex > 0) _historyIndex--;

                    senderBox.Text = _commandHistory[_historyIndex];
                    e.Handled = true;
                }
            }

            if (e.Key == VirtualKey.Down)
            {
                if (_historyIndex != -1)
                {
                    if (_historyIndex < _commandHistory.Count - 1)
                    {
                        _historyIndex++;
                        senderBox.Text = _commandHistory[_historyIndex];
                    }
                    else
                    {
                        _historyIndex = -1;
                        senderBox.Text = "";
                    }
                    e.Handled = true;
                }
            }
        }

        protected async void BoxCommand_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            string command = string.IsNullOrEmpty(args.QueryText) ? sender.Text : args.QueryText;
            if (string.IsNullOrWhiteSpace(command)) return;

            if (_commandHistory.Count == 0 || _commandHistory.Last() != command)
            {
                _commandHistory.Add(command);
            }
            _historyIndex = -1;

            _vm.SendCommand(NormalizeMcCommand(command));

            sender.Text = "";
            _cachedGhostText.Text = "";
            _currentPrediction = "";
            sender.Focus(FocusState.Programmatic);

            await ResumeAutoScroll();
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
            _vm.SendCommand(NormalizeMcCommand(BoxCommand.Text));
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
                _cachedScrollViewer = sv;
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

            isAtBottom = sv.VerticalOffset >= (sv.ScrollableHeight - 40);

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

        protected async Task ResumeAutoScroll()
        {
            if (_cachedScrollViewer != null && !isAtBottom)
            {
                _ignoreNextViewChanged = true;
                _autoScroll = true;

                if (ResumeAutoScrollButton != null)
                    ResumeAutoScrollButton.Visibility = Visibility.Collapsed;

                LogList.UpdateLayout();

                await Task.Delay(1);

                try
                {
                    _cachedScrollViewer.ChangeView(null, _cachedScrollViewer.ScrollableHeight, null, true);
                }
                catch {}
            }
        }

        protected void ResumeAutoScroll_Click(object sender, RoutedEventArgs e)
        {
            ResumeAutoScroll();
        }

        private async void ScrollToBottomIfNeeded()
        {
            if (!_autoScroll || _cachedScrollViewer == null)
                return;

            try
            {
                await Dispatcher.RunAsync(CoreDispatcherPriority.Low, () =>
                {
                    _cachedScrollViewer.ChangeView(
                        null,
                        _cachedScrollViewer.ScrollableHeight,
                        null,
                        true
                    );
                });
            }
            catch { }
        }

        // Search + Filtering
        protected void Search_Click(object sender, RoutedEventArgs e)
        {
            if (SearchBox.Visibility == Visibility.Visible)
            {
                SearchBox.Visibility = Visibility.Collapsed;
                SearchBox.Text = "";
                _vm.CurrentSearchQuery = "";
                _vm.ApplyFilter();
                ScrollToBottomIfNeeded();
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
            _vm.CurrentSearchQuery = query;
            _vm.ApplyFilter();
            ScrollToBottomIfNeeded();
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
            {
                string content;

                if (x.Segments != null && x.Segments.Count > 0)
                {
                    content = string.Join("", x.Segments.Select(s => s.Text));
                }
                else
                {
                    content = x.Message;
                }

                return $"{x.Timestamp:HH:mm:ss} [{x.Level}] {content}";
            }));

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
            {
                _vm.AllLogs.Remove(entry);
                _vm.VisibleLogs.Remove(entry);
            }
        }

        private ToggleMenuFlyoutItem CreateFilterItem(string level)
        {
            var item = new ToggleMenuFlyoutItem
            {
                Text = level,
                Tag = level,
                IsChecked = _vm.EnabledLevels.Contains(level)
            };
            item.Click += FilterMenu_Click;
            return item;
        }

        private void FilterMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is ToggleMenuFlyoutItem item && item.Tag is string level)
            {
                if (item.IsChecked)
                    _vm.EnabledLevels.Add(level);
                else
                    _vm.EnabledLevels.Remove(level);

                _vm.ApplyFilter();
                ScrollToBottomIfNeeded();
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