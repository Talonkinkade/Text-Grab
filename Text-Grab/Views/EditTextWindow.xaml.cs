﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using Text_Grab.Controls;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Utilities;
using Text_Grab.Views;
using Windows.ApplicationModel.DataTransfer;
using Windows.Globalization;
using Windows.System;
using MessageBox = System.Windows.MessageBox;

namespace Text_Grab;

/// <summary>
/// Interaction logic for ManipulateTextWindow.xaml
/// </summary>

public partial class EditTextWindow : Window
{
    private string? OpenedFilePath;

    private CultureInfo selectedCultureInfo = CultureInfo.CurrentCulture;

    public CurrentCase CaseStatusOfToggle { get; set; } = CurrentCase.Unknown;

    public bool WrapText { get; set; } = false;

    public bool LaunchedFromNotification = false;

    public static RoutedCommand SplitOnSelectionCmd = new();

    public static RoutedCommand IsolateSelectionCmd = new();

    public static RoutedCommand SingleLineCmd = new();

    public static RoutedCommand ToggleCaseCmd = new();

    public static RoutedCommand ReplaceReservedCmd = new();

    public static RoutedCommand UnstackCmd = new();

    public static RoutedCommand UnstackGroupCmd = new();

    public static RoutedCommand DeleteAllSelectionCmd = new();

    public static RoutedCommand DeleteAllSelectionPatternCmd = new();

    public static RoutedCommand InsertSelectionOnEveryLineCmd = new();

    private int numberOfContextMenuItems;

    private List<string> imageExtensions = new() { ".png", ".bmp", ".jpg", ".jpeg", ".tiff", ".gif" };

    private bool _IsAccessingClipboard { get; set; } = false;

    private WindowState? prevWindowState;

    public EditTextWindow()
    {
        InitializeComponent();
    }

    public EditTextWindow(string possiblyEndcodedString, bool isEncoded = true)
    {
        InitializeComponent();

        if (isEncoded)
        {
            string rawEncodedString = possiblyEndcodedString.Substring(5);
            try
            {
                // restore the padding '=' in base64 string
                switch (rawEncodedString.Length % 4)
                {
                    case 2: rawEncodedString += "=="; break;
                    case 3: rawEncodedString += "="; break;
                }
                byte[] encodedBytes = Convert.FromBase64String(rawEncodedString);
                string copiedText = Encoding.UTF8.GetString(encodedBytes);
                PassedTextControl.Text = copiedText;
            }
            catch (Exception ex)
            {
                PassedTextControl.Text = rawEncodedString;
                PassedTextControl.Text += ex.Message;
            }
        }
        else
        {
            PassedTextControl.Text = possiblyEndcodedString;
        }

        LaunchedFromNotification = true;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        RoutedCommand newFullscreenGrab = new();
        _ = newFullscreenGrab.InputGestures.Add(new KeyGesture(Key.F, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(newFullscreenGrab, keyedCtrlF));

        RoutedCommand newGrabFrame = new();
        _ = newGrabFrame.InputGestures.Add(new KeyGesture(Key.G, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(newGrabFrame, keyedCtrlG));

        RoutedCommand selectLineCommand = new();
        _ = selectLineCommand.InputGestures.Add(new KeyGesture(Key.L, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(selectLineCommand, SelectLine));

        RoutedCommand IsolateSelectionCommand = new();
        _ = IsolateSelectionCommand.InputGestures.Add(new KeyGesture(Key.I, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(IsolateSelectionCommand, IsolateSelectionCmdExecuted));

        RoutedCommand SaveCommand = new();
        _ = SaveCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(SaveCommand, SaveBTN_Click));

        RoutedCommand SaveAsCommand = new();
        _ = SaveAsCommand.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Shift | ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(SaveAsCommand, SaveAsBTN_Click));

        RoutedCommand OpenCommand = new();
        _ = OpenCommand.InputGestures.Add(new KeyGesture(Key.O, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(OpenCommand, OpenFileMenuItem_Click));

        RoutedCommand moveLineUpCommand = new();
        _ = moveLineUpCommand.InputGestures.Add(new KeyGesture(Key.Up, ModifierKeys.Alt));
        _ = CommandBindings.Add(new CommandBinding(moveLineUpCommand, MoveLineUp));

        RoutedCommand moveLineDownCommand = new();
        _ = moveLineDownCommand.InputGestures.Add(new KeyGesture(Key.Down, ModifierKeys.Alt));
        _ = CommandBindings.Add(new CommandBinding(moveLineDownCommand, MoveLineDown));

        RoutedCommand toggleCaseCommand = new();
        _ = toggleCaseCommand.InputGestures.Add(new KeyGesture(Key.F3, ModifierKeys.Shift));
        _ = CommandBindings.Add(new CommandBinding(toggleCaseCommand, ToggleCase));

        RoutedCommand replaceReservedCharsCommand = new();
        _ = replaceReservedCharsCommand.InputGestures.Add(new KeyGesture(Key.R, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(replaceReservedCharsCommand, ReplaceReservedCharsCmdExecuted));

        RoutedCommand UnstackCommand = new();
        _ = UnstackCommand.InputGestures.Add(new KeyGesture(Key.U, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(UnstackCommand, UnstackExecuted));

        RoutedCommand NewLookupCommand = new();
        _ = NewLookupCommand.InputGestures.Add(new KeyGesture(Key.Q, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(NewLookupCommand, LaunchQuickSimpleLookup));

        RoutedCommand selectWordCommand = new();
        _ = selectWordCommand.InputGestures.Add(new KeyGesture(Key.W, ModifierKeys.Control));
        _ = CommandBindings.Add(new CommandBinding(selectWordCommand, SelectWord));

        RoutedCommand EscapeKeyed = new();
        _ = EscapeKeyed.InputGestures.Add(new KeyGesture(Key.Escape));
        _ = CommandBindings.Add(new CommandBinding(EscapeKeyed, KeyedEscape));

        PassedTextControl.ContextMenu = this.FindResource("ContextMenuResource") as ContextMenu;
        if (PassedTextControl.ContextMenu != null)
            numberOfContextMenuItems = PassedTextControl.ContextMenu.Items.Count;

        string inputLang = InputLanguageManager.Current.CurrentInputLanguage.Name;
        XmlLanguage lang = XmlLanguage.GetLanguage(inputLang);
        selectedCultureInfo = lang.GetEquivalentCulture();
        if (selectedCultureInfo.TextInfo.IsRightToLeft)
        {
            PassedTextControl.TextAlignment = TextAlignment.Right;
        }

        if (Settings.Default.EditWindowStartFullscreen
            && string.IsNullOrWhiteSpace(OpenedFilePath)
            && !LaunchedFromNotification)
        {
            WindowUtilities.LaunchFullScreenGrab(true, false, PassedTextControl);
            LaunchFullscreenOnLoad.IsChecked = true;
            prevWindowState = this.WindowState;
            WindowState = WindowState.Minimized;
        }

        if (Settings.Default.EditWindowIsOnTop)
        {
            AlwaysOnTop.IsChecked = true;
            Topmost = true;
        }

        if (!Settings.Default.EditWindowIsWordWrapOn)
        {
            WrapTextMenuItem.IsChecked = false;
            PassedTextControl.TextWrapping = TextWrapping.NoWrap;
        }

        if (Settings.Default.EditWindowBottomBarIsHidden)
        {
            HideBottomBarMenuItem.IsChecked = true;
            BottomBar.Visibility = Visibility.Collapsed;
        }

        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged -= Clipboard_ContentChanged;
        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged += Clipboard_ContentChanged;
    }

    private void KeyedEscape(object sender, ExecutedRoutedEventArgs e)
    {
        if (cancellationTokenForDirOCR is not null)
            cancellationTokenForDirOCR.Cancel();
    }

    private async void Clipboard_ContentChanged(object? sender, object e)
    {
        if (ClipboardWatcherMenuItem.IsChecked is false || _IsAccessingClipboard)
            return;

        _IsAccessingClipboard = true;
        DataPackageView? dataPackageView = null;

        try
        {
            dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        }
        catch (System.Exception ex)
        {
            Debug.WriteLine($"error with Windows.ApplicationModel.DataTransfer.Clipboard.GetContent(). Exception Message: {ex.Message}");
        }

        if (dataPackageView is not null && dataPackageView.Contains(StandardDataFormats.Text))
        {
            string text = string.Empty;
            try
            {
                text = await dataPackageView.GetTextAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(new Action(() => { AddCopiedTextToTextBox(text); }));
            }
            catch (System.Exception ex)
            {
                Debug.WriteLine($"error with dataPackageView.GetTextAsync(). Exception Message: {ex.Message}");
            }
        };

        _IsAccessingClipboard = false;
    }

    private void AddCopiedTextToTextBox(string textToAdd)
    {
        PassedTextControl.AppendText(Environment.NewLine + textToAdd);
    }

    private void Window_Initialized(object sender, EventArgs e)
    {
        PassedTextControl.PreviewMouseWheel += HandlePreviewMouseWheel;
        WindowUtilities.SetWindowPosition(this);
        SetFontFromSettings();
    }

    private void SetFontFromSettings()
    {
        PassedTextControl.FontFamily = new System.Windows.Media.FontFamily(Settings.Default.FontFamilySetting);
        PassedTextControl.FontSize = Settings.Default.FontSizeSetting;
        if (Settings.Default.IsFontBold)
            PassedTextControl.FontWeight = FontWeights.Bold;
        if (Settings.Default.IsFontItalic)
            PassedTextControl.FontStyle = FontStyles.Italic;

        TextDecorationCollection tdc = new();
        if (Settings.Default.IsFontUnderline) tdc.Add(TextDecorations.Underline);
        if (Settings.Default.IsFontStrikeout) tdc.Add(TextDecorations.Strikethrough);
        PassedTextControl.TextDecorations = tdc;
    }

    private void PassedTextControl_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (Settings.Default.EditWindowStartFullscreen && prevWindowState is not null)
        {
            this.WindowState = prevWindowState.Value;
            prevWindowState = null;
        }

        UpdateLineAndColumnText();
    }

    private void SelectionContainsNewLinesCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (PassedTextControl.SelectedText.Contains(Environment.NewLine)
            || PassedTextControl.SelectedText.Contains('\r')
            || PassedTextControl.SelectedText.Contains('\n'))
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void ToggleCaseCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        string text;
        bool containsLetters = false;

        if (PassedTextControl.SelectionLength == 0)
            text = PassedTextControl.Text;
        else
            text = PassedTextControl.SelectedText;

        foreach (char letter in text)
        {
            if (char.IsLetter(letter))
                containsLetters = true;
        }

        if (containsLetters)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }


    private void ToggleCase(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string textToModify;

        if (PassedTextControl.SelectionLength == 0)
            textToModify = PassedTextControl.Text;
        else
            textToModify = PassedTextControl.SelectedText;

        if (CaseStatusOfToggle == CurrentCase.Unknown)
            CaseStatusOfToggle = DetermineToggleCase(textToModify);

        TextInfo currentTI = selectedCultureInfo.TextInfo;

        switch (CaseStatusOfToggle)
        {
            case CurrentCase.Lower:
                textToModify = currentTI.ToLower(textToModify);
                CaseStatusOfToggle = CurrentCase.Camel;
                break;
            case CurrentCase.Camel:
                textToModify = currentTI.ToTitleCase(textToModify);
                CaseStatusOfToggle = CurrentCase.Upper;
                break;
            case CurrentCase.Upper:
                textToModify = currentTI.ToUpper(textToModify);
                CaseStatusOfToggle = CurrentCase.Lower;
                break;
            default:
                break;
        }

        if (PassedTextControl.SelectionLength == 0)
            PassedTextControl.Text = textToModify;
        else
            PassedTextControl.SelectedText = textToModify;
    }

    private static CurrentCase DetermineToggleCase(string textToModify)
    {
        bool isAllLower = true;
        bool isAllUpper = true;

        foreach (char letter in textToModify)
        {
            if (char.IsLower(letter))
            {
                isAllUpper = false;
            }
            if (char.IsUpper(letter))
            {
                isAllLower = false;
            }
        }

        if (!isAllLower
            && isAllUpper)
            return CurrentCase.Lower;

        return CurrentCase.Camel;
    }

    private void OpenFileMenuItem_Click(object sender, RoutedEventArgs e)
    {
        // Create OpenFileDialog 
        Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

        // Set filter for file extension and default file extension 
        dlg.DefaultExt = ".txt";
        dlg.Filter = "Text documents (.txt)|*.txt";

        bool? result = dlg.ShowDialog();

        if (result is bool isTrueResult
            && File.Exists(dlg.FileName))
        {
            OpenThisPath(dlg.FileName);
        }
    }

    internal async void OpenThisPath(string pathOfFileToOpen, bool isMultipleFiles = false)
    {
        OpenedFilePath = pathOfFileToOpen;

        StringBuilder stringBuilder = new();

        if (isMultipleFiles)
        {
            stringBuilder.AppendLine(pathOfFileToOpen);
        }

        if (imageExtensions.Contains(Path.GetExtension(OpenedFilePath).ToLower()))
        {
            try
            {
                stringBuilder.Append(await ImageMethods.OcrAbsoluteFilePath(OpenedFilePath));
            }
            catch (Exception)
            {
                System.Windows.MessageBox.Show("Failed to read file");
            }
        }
        else
        {
            // Continue with along trying to open a text file.

            if (!isMultipleFiles && string.IsNullOrWhiteSpace(PassedTextControl.Text))
                Title = $"Edit Text | {Path.GetFileName(OpenedFilePath)}";

            try
            {
                using StreamReader sr = File.OpenText(pathOfFileToOpen);

                string s = await sr.ReadToEndAsync();

                stringBuilder.Append(s);
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Failed to open file. {ex.Message}");
            }
        }

        if (isMultipleFiles)
        {
            stringBuilder.Append(Environment.NewLine);
            stringBuilder.Append(Environment.NewLine);
        }

        PassedTextControl.AppendText(stringBuilder.ToString());
    }

    private void MoveLineDown(object? sender, ExecutedRoutedEventArgs? e)
    {
        SelectLine(sender, e);

        string lineText = PassedTextControl.SelectedText;
        PassedTextControl.SelectedText = "";
        string textBoxText = PassedTextControl.Text;
        int selectionIndex = PassedTextControl.SelectionStart;
        int indexOfNextNewline = textBoxText.Length;

        if (!PassedTextControl.Text.EndsWith(Environment.NewLine))
        {
            PassedTextControl.Text += Environment.NewLine;
        }

        IEnumerable<int> indiciesOfNewLine = textBoxText.AllIndexesOf(Environment.NewLine);

        foreach (int newLineIndex in indiciesOfNewLine)
        {
            int newLineEnd = newLineIndex;
            if (newLineEnd >= selectionIndex)
            {
                indexOfNextNewline = newLineEnd + Environment.NewLine.Length;
                break;
            }
        }

        PassedTextControl.Select(indexOfNextNewline, 0);
        PassedTextControl.SelectedText = lineText;
    }

    private void MoveLineUp(object? sender, ExecutedRoutedEventArgs? e)
    {
        SelectLine(sender, e);
        string lineText = PassedTextControl.SelectedText;
        PassedTextControl.SelectedText = "";
        string textBoxText = PassedTextControl.Text;
        int selectionIndex = PassedTextControl.SelectionStart;
        int indexOfPreviousNewline = 0;

        IEnumerable<int> indiciesOfNewLine = textBoxText.AllIndexesOf(Environment.NewLine);

        foreach (int newLineIndex in indiciesOfNewLine)
        {
            int newLineEnd = newLineIndex + Environment.NewLine.Length;
            if (newLineEnd < selectionIndex)
                indexOfPreviousNewline = newLineEnd;
        }

        PassedTextControl.Select(indexOfPreviousNewline, 0);
        PassedTextControl.SelectedText = lineText;
    }

    private void keyedCtrlF(object sender, ExecutedRoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(openAnyway: true, destinationTextBox: PassedTextControl);
    }

    private void keyedCtrlG(object sender, ExecutedRoutedEventArgs e)
    {
        CheckForGrabFrameOrLaunch();
    }

    private void CopyCloseBTN_Click(object sender, RoutedEventArgs e)
    {
        string clipboardText = PassedTextControl.Text;
        try { System.Windows.Clipboard.SetDataObject(clipboardText, true); } catch { }
        this.Close();
    }

    private void SaveBTN_Click(object sender, RoutedEventArgs e)
    {
        string fileText = PassedTextControl.Text;

        if (string.IsNullOrEmpty(OpenedFilePath))
        {
            Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog()
            {
                Filter = "Text Files(*.txt)|*.txt|All(*.*)|*",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                RestoreDirectory = true,
            };

            if (dialog.ShowDialog() is bool isOkayOrAccept)
            {
                File.WriteAllText(dialog.FileName, fileText);
                OpenedFilePath = dialog.FileName;
                Title = $"Edit Text | {OpenedFilePath.Split('\\').LastOrDefault()}";
            }
        }
        else
        {
            File.WriteAllText(OpenedFilePath, fileText);
        }
    }

    private void NewWindow_Clicked(object sender, RoutedEventArgs e)
    {
        EditTextWindow newETW = new();
        newETW.Show();
    }

    private void NewWindowWithText_Clicked(object sender, RoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;
        PassedTextControl.SelectedText = "";
        EditTextWindow newETWwithText = new(selectedText, false);
        newETWwithText.Show();
    }

    private void SaveAsBTN_Click(object sender, RoutedEventArgs e)
    {
        string fileText = PassedTextControl.Text;

        Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog()
        {
            Filter = "Text Files(*.txt)|*.txt|All(*.*)|*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            RestoreDirectory = true,
        };

        if (dialog.ShowDialog() is bool isOkayOrAccept)
        {
            File.WriteAllText(dialog.FileName, fileText);
            OpenedFilePath = dialog.FileName;
            Title = $"Edit Text | {OpenedFilePath.Split('\\').LastOrDefault()}";
        }
    }

    private void SingleLineCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (PassedTextControl.SelectedText.Length > 0)
            PassedTextControl.SelectedText = PassedTextControl.SelectedText.MakeStringSingleLine();
        else
            PassedTextControl.Text = PassedTextControl.Text.MakeStringSingleLine();
    }

    private void SingleLineCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        string textToOperateOn;

        if (PassedTextControl.SelectedText.Length > 0)
            textToOperateOn = PassedTextControl.SelectedText;
        else
            textToOperateOn = PassedTextControl.Text;


        if (textToOperateOn.Contains(Environment.NewLine)
            || textToOperateOn.Contains('\r')
            || textToOperateOn.Contains('\n'))
        {
            e.CanExecute = true;
            return;
        }

        e.CanExecute = false;
    }

    private void WrapTextCHBOX_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if ((bool)WrapTextMenuItem.IsChecked)
            PassedTextControl.TextWrapping = TextWrapping.Wrap;
        else
            PassedTextControl.TextWrapping = TextWrapping.NoWrap;

        Settings.Default.EditWindowIsWordWrapOn = (bool)WrapTextMenuItem.IsChecked;
    }

    private void TrimEachLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string workingString = PassedTextControl.Text;
        string[] stringSplit = workingString.Split(Environment.NewLine);

        string finalString = "";
        foreach (string line in stringSplit)
        {
            if (!string.IsNullOrWhiteSpace(line))
                finalString += line.Trim() + Environment.NewLine;
        }

        PassedTextControl.Text = finalString;
    }

    public void AddThisText(string textToAdd)
    {
        PassedTextControl.AppendText(textToAdd);
    }

    private void UnstackExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selection = Regex.Replace(PassedTextControl.SelectedText, @"(\r\n|\n|\r)", Environment.NewLine);
        string[] selectionLines = selection.Split(Environment.NewLine);
        int numberOfLines = selectionLines.Length;

        PassedTextControl.Text = PassedTextControl.Text.UnstackStrings(numberOfLines);
    }

    private void UnstackGroupExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selection = Regex.Replace(PassedTextControl.SelectedText, @"(\r\n|\n|\r)", Environment.NewLine);
        string[] selectionLines = selection.Split(Environment.NewLine);
        int numberOfLines = selectionLines.Length;

        PassedTextControl.Text = PassedTextControl.Text.UnstackGroups(numberOfLines);
    }

    private void DeleteAllSelectionExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selectionToDelete = PassedTextControl.SelectedText;

        PassedTextControl.Text = PassedTextControl.Text.RemoveAllInstancesOf(selectionToDelete);
    }

    private void DeleteAllSelectionPatternExecuted(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selectionToDelete = PassedTextControl.SelectedText;
        string Pattern = selectionToDelete.ExtractSimplePattern();
        MatchCollection Matches = Regex.Matches(PassedTextControl.Text, Pattern, RegexOptions.Multiline);
        StringBuilder sb = new(PassedTextControl.Text);
        for (int i = Matches.Count - 1; i >= 0; i--)
        {
            Match match = Matches[i];

            sb.Remove(match.Index, match.Length);
        }

        PassedTextControl.Text = sb.ToString();
    }

    private void InsertSelectionOnEveryLineCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PassedTextControl.SelectedText)
            || PassedTextControl.SelectedText.Contains(Environment.NewLine)
            || PassedTextControl.SelectedText.Contains('\r')
            || PassedTextControl.SelectedText.Contains('\n'))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void InsertSelectionOnEveryLine(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string[] splitString = PassedTextControl.Text.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);
        string selectionText = PassedTextControl.SelectedText;
        int initialSelectionStart = PassedTextControl.SelectionStart;
        int selectionPositionInLine = PassedTextControl.SelectionStart;
        for (int i = initialSelectionStart; i >= 0; i--)
        {
            if (PassedTextControl.Text[i] == '\n'
                || PassedTextControl.Text[i] == '\r')
            {
                selectionPositionInLine = initialSelectionStart - i - 1;
                break;
            }
        }

        int selectionLength = PassedTextControl.SelectionLength;

        if (string.IsNullOrEmpty(splitString.Last()))
            splitString = splitString.SkipLast(1).ToArray();

        StringBuilder sb = new();
        foreach (string line in splitString)
        {
            if (line.Length >= selectionPositionInLine
                && line.Length >= (selectionPositionInLine + selectionLength))
            {
                if (line.Substring(selectionPositionInLine, selectionLength) != selectionText)
                    sb.Append(line.Insert(selectionPositionInLine, selectionText));
                else
                    sb.Append(line);
            }
            else
            {
                if (line.Length > selectionPositionInLine)
                    sb.Append(line.Insert(selectionPositionInLine, selectionText));
                else
                    sb.Append(line).Append(selectionText.PadLeft((selectionPositionInLine + selectionLength) - line.Length));
            }
            sb.Append(Environment.NewLine);
        }

        PassedTextControl.Text = sb.ToString();
    }

    private void TryToNumberMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string workingString = string.Empty;

        if (PassedTextControl.SelectionLength == 0)
            workingString = PassedTextControl.Text;
        else
            workingString = PassedTextControl.SelectedText;

        workingString = workingString.TryFixToNumbers();

        if (PassedTextControl.SelectionLength == 0)
            PassedTextControl.Text = workingString;
        else
            PassedTextControl.SelectedText = workingString;
    }
    private void TryToAlphaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        string workingString = string.Empty;

        if (PassedTextControl.SelectionLength == 0)
            workingString = PassedTextControl.Text;
        else
            workingString = PassedTextControl.SelectedText;

        workingString = workingString.TryFixToLetters();

        if (PassedTextControl.SelectionLength == 0)
            PassedTextControl.Text = workingString;
        else
            PassedTextControl.SelectedText = workingString;
    }

    private void SplitOnSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        string selectedText = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(selectedText))
        {
            System.Windows.MessageBox.Show("No text selected", "Did not split lines");
            return;
        }

        StringBuilder textToManipulate = new StringBuilder(PassedTextControl.Text);

        textToManipulate = textToManipulate.Replace(selectedText, Environment.NewLine + selectedText);

        PassedTextControl.Text = textToManipulate.ToString();
    }

    private void SplitOnSelectionCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PassedTextControl.SelectedText))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void IsolateSelectionCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(PassedTextControl.SelectedText))
            PassedTextControl.Text = PassedTextControl.SelectedText;
    }

    private void IsolateSelectionCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(PassedTextControl.SelectedText))
            e.CanExecute = false;
        else
            e.CanExecute = true;
    }

    private void CheckForGrabFrameOrLaunch()
    {
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is GrabFrame grabFrame)
            {
                grabFrame.Activate();
                grabFrame.IsFromEditWindow = true;
                return;
            }
        }
        Keyboard.Focus(PassedTextControl);
        PassedTextControl.IsInactiveSelectionHighlightEnabled = true;
        PassedTextControl.SelectedText = " ";
        CopyCloseBTN.Focus();
        GrabFrame gf = new();
        gf.IsFromEditWindow = true;
        gf.DestinationTextBox = PassedTextControl;
        gf.Show();
    }

    private void OpenGrabFrame_Click(object sender, RoutedEventArgs e)
    {
        CheckForGrabFrameOrLaunch();
    }

    public System.Windows.Controls.TextBox GetMainTextBox()
    {
        return PassedTextControl;
    }

    private void NewFullscreen_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(true, destinationTextBox: PassedTextControl);
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is SettingsWindow sw)
            {
                sw.Activate();
                return;
            }
        }

        SettingsWindow nsw = new();
        nsw.Show();
    }

    private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void AlwaysOnTop_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is MenuItem aotMi && aotMi.IsChecked)
            Topmost = true;
        else
            Topmost = false;

        Settings.Default.EditWindowIsOnTop = Topmost;
    }

    private void HideBottomBarMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
            return;

        if (sender is MenuItem bbMi && bbMi.IsChecked)
        {
            BottomBar.Visibility = Visibility.Collapsed;
            Settings.Default.EditWindowBottomBarIsHidden = true;
        }
        else
        {
            BottomBar.Visibility = Visibility.Visible;
            Settings.Default.EditWindowBottomBarIsHidden = false;
        }
    }

    private void FeedbackMenuItem_Click(object sender, RoutedEventArgs ev)
    {
        Uri source = new("https://github.com/TheJoeFin/Text-Grab/issues", UriKind.Absolute);
        RequestNavigateEventArgs e = new(source, "https://github.com/TheJoeFin/Text-Grab/issues");
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void FontMenuItem_Click(object sender, RoutedEventArgs e)
    {
        using (FontDialog fd = new())
        {
            Font currentFont = new(PassedTextControl.FontFamily.ToString(), (float)((PassedTextControl.FontSize * 72.0) / 96.0));
            fd.Font = currentFont;
            var result = fd.ShowDialog();
            if (result == System.Windows.Forms.DialogResult.OK)
            {
                Debug.WriteLine(fd.Font);

                Settings.Default.FontFamilySetting = fd.Font.Name;
                Settings.Default.FontSizeSetting = (fd.Font.Size * 96.0 / 72.0);
                Settings.Default.IsFontBold = fd.Font.Bold;
                Settings.Default.IsFontItalic = fd.Font.Italic;
                Settings.Default.IsFontUnderline = fd.Font.Underline;
                Settings.Default.IsFontStrikeout = fd.Font.Strikeout;
                Settings.Default.Save();

                SetFontFromSettings();
            }
        }
    }

    private void SelectWordMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SelectWord();
    }

    private void SelectWord(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        (int wordStart, int wordLength) = PassedTextControl.Text.CursorWordBoundaries(PassedTextControl.CaretIndex);

        PassedTextControl.Select(wordStart, wordLength);
    }

    private void SelectLine(object? sender = null, ExecutedRoutedEventArgs? e = null)
    {
        string selectedText = PassedTextControl.SelectedText;
        int selectionIndex = PassedTextControl.SelectionStart;
        int selectionEndIndex = PassedTextControl.SelectionStart + PassedTextControl.SelectionLength;
        if (selectedText.EndsWith(Environment.NewLine))
            selectionEndIndex -= Environment.NewLine.Length;
        int selectionLength = PassedTextControl.SelectionLength;
        string textBoxText = PassedTextControl.Text;

        if (!textBoxText.EndsWith(Environment.NewLine))
            textBoxText += Environment.NewLine;

        IEnumerable<int> allNewLines = textBoxText.AllIndexesOf(Environment.NewLine);
        int lastLine = allNewLines.LastOrDefault();
        bool foundEnd = false;

        int startSelectionIndex = 0;
        int stopSelectionIndex = 0;

        foreach (int newLineIndex in allNewLines)
        {
            if (selectionIndex > newLineIndex)
                startSelectionIndex = newLineIndex + Environment.NewLine.Length;

            if (!foundEnd
                && newLineIndex >= selectionEndIndex)
            {
                stopSelectionIndex = newLineIndex;
                foundEnd = true;
            }
        }

        if (selectionEndIndex > lastLine)
            stopSelectionIndex = textBoxText.Length;

        selectionLength = stopSelectionIndex - startSelectionIndex + Environment.NewLine.Length;
        if (selectionLength < 0)
            selectionLength = 0;

        PassedTextControl.Select(startSelectionIndex, selectionLength);
    }

    private void LaunchFindAndReplace()
    {
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is FindAndReplaceWindow openFindReplaceWindow)
            {
                openFindReplaceWindow.Activate();
                return;
            }
        }

        FindAndReplaceWindow farw = new();
        farw.StringFromWindow = PassedTextControl.Text;
        farw.TextEditWindow = this;
        farw.Show();

        if (PassedTextControl.SelectedText.Length > 0)
        {
            farw.FindTextBox.Text = PassedTextControl.SelectedText.Trim();
            farw.SearchForText();
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        LaunchFindAndReplace();
    }

    private void LaunchFullscreenOnLoad_Click(object sender, RoutedEventArgs e)
    {
        Settings.Default.EditWindowStartFullscreen = LaunchFullscreenOnLoad.IsChecked;
        Settings.Default.Save();
    }

    private void Window_Activated(object sender, EventArgs e)
    {
        PassedTextControl.Focus();
    }

    // private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    private void Window_Closed(object? sender, EventArgs e)
    {
        string windowSizeAndPosition = $"{this.Left},{this.Top},{this.Width},{this.Height}";
        Properties.Settings.Default.EditTextWindowSizeAndPosition = windowSizeAndPosition;
        Properties.Settings.Default.Save();

        Windows.ApplicationModel.DataTransfer.Clipboard.ContentChanged -= Clipboard_ContentChanged;

        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is GrabFrame grabFrame)
            {
                grabFrame.IsFromEditWindow = false;
            }
            if (window is FullscreenGrab fullscreenGrab)
            {
                fullscreenGrab.DestinationTextBox = null;
            }
            if (window is FindAndReplaceWindow findAndReplaceWindow)
            {
                findAndReplaceWindow.ShouldCloseWithThisETW(this);
            }
        }

        WindowUtilities.ShouldShutDown();
    }

    private void ReplaceReservedCharsCmdExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (PassedTextControl.SelectionLength > 0)
        {
            PassedTextControl.SelectedText = PassedTextControl.SelectedText.ReplaceReservedCharacters();
        }
        else
        {
            PassedTextControl.Text = PassedTextControl.Text.ReplaceReservedCharacters();
        }
    }

    private void ReplaceReservedCharsCmdCanExecute(object sender, CanExecuteRoutedEventArgs e)
    {
        bool containsAnyReservedChars = false;

        if (PassedTextControl.SelectionLength > 0)
        {
            foreach (char reservedChar in StringMethods.ReservedChars)
            {
                if (PassedTextControl.SelectedText.Contains(reservedChar))
                    containsAnyReservedChars = true;
            }
        }
        else
        {
            foreach (char reservedChar in StringMethods.ReservedChars)
            {
                if (PassedTextControl.Text.Contains(reservedChar))
                    containsAnyReservedChars = true;
            }
        }

        if (containsAnyReservedChars)
            e.CanExecute = true;
        else
            e.CanExecute = false;
    }

    private void HandlePreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        // Source: StackOverflow, read on Sep. 10, 2021
        // https://stackoverflow.com/a/53698638/7438031

        if (Keyboard.Modifiers != ModifierKeys.Control)
            return;

        e.Handled = true;

        if (e.Delta > 0)
            PassedTextControl.FontSize += 4;
        else if (e.Delta < 0)
        {
            if (PassedTextControl.FontSize > 4)
                PassedTextControl.FontSize -= 4;
        }
    }

    private void SelectLineMenuItem_Click(object sender, RoutedEventArgs e)
    {
        SelectLine();
    }

    private void MoveLineUpMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveLineUp(sender, null);
    }

    private void MoveLineDownMenuItem_Click(object sender, RoutedEventArgs e)
    {
        MoveLineDown(sender, null);
    }

    private void FindAndReplaceMenuItem_Click(object sender, RoutedEventArgs e)
    {
        LaunchFindAndReplace();
    }

    private async void RateAndReview_Click(object sender, RoutedEventArgs e)
    {
        _ = await Launcher.LaunchUriAsync(new Uri(string.Format("ms-windows-store:REVIEW?PFN={0}", "40087JoeFinApps.TextGrab_kdbpvth5scec4")));
    }

    private async void ContactMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _ = await Launcher.LaunchUriAsync(new Uri(string.Format("mailto:support@textgrab.net")));
    }

    private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowCollection allWindows = System.Windows.Application.Current.Windows;

        foreach (Window window in allWindows)
        {
            if (window is FirstRunWindow firstRunWindowOpen)
            {
                firstRunWindowOpen.Activate();
                return;
            }
        }

        FirstRunWindow frw = new();
        frw.Show();
    }


    private void FullScreenGrabMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WindowUtilities.LaunchFullScreenGrab(true, destinationTextBox: PassedTextControl);
    }

    private void GrabFrameMenuItem_Click(object sender, RoutedEventArgs e)
    {
        CheckForGrabFrameOrLaunch();
    }

    private void PassedTextControl_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        PassedTextControl.ContextMenu = null;

        int caretIndex, cmdIndex;
        SpellingError spellingError;

        ContextMenu? baseContextMenu = this.FindResource("ContextMenuResource") as ContextMenu;

        while (baseContextMenu != null
            && baseContextMenu.Items.Count > numberOfContextMenuItems)
        {
            baseContextMenu.Items.RemoveAt(0);
        }

        PassedTextControl.ContextMenu = baseContextMenu;
        caretIndex = PassedTextControl.CaretIndex;

        string possibleURL = PassedTextControl.SelectedText;

        if (string.IsNullOrEmpty(possibleURL))
        {
            (int wordStart, int wordLength) = PassedTextControl.Text.CursorWordBoundaries(caretIndex);
            possibleURL = PassedTextControl.Text.Substring(wordStart, wordLength);
        }

        cmdIndex = 0;
        spellingError = PassedTextControl.GetSpellingError(caretIndex);
        if (spellingError != null
            && PassedTextControl.ContextMenu != null)
        {
            foreach (string str in spellingError.Suggestions)
            {
                MenuItem mi = new();
                mi.Header = str;
                mi.FontWeight = FontWeights.Bold;
                mi.Command = EditingCommands.CorrectSpellingError;
                mi.CommandParameter = str;
                mi.CommandTarget = PassedTextControl;
                PassedTextControl.ContextMenu.Items.Insert(cmdIndex, mi);
                cmdIndex++;
            }

            if (cmdIndex == 0)
            {
                MenuItem mi = new();
                mi.Header = "no suggestions";
                mi.IsEnabled = false;
                PassedTextControl.ContextMenu.Items.Insert(cmdIndex, mi);
                cmdIndex++;
            }

            PassedTextControl.ContextMenu.Items.Insert(cmdIndex, new Separator());
            cmdIndex++;
            MenuItem ignoreAllMI = new();
            ignoreAllMI.Header = "Ignore All";
            ignoreAllMI.Command = EditingCommands.IgnoreSpellingError;
            ignoreAllMI.CommandTarget = PassedTextControl;
            PassedTextControl.ContextMenu.Items.Insert(cmdIndex, ignoreAllMI);
            cmdIndex++;
            PassedTextControl.ContextMenu.Items.Insert(cmdIndex, new Separator());
        }

        if (Uri.TryCreate(possibleURL, UriKind.Absolute, out var uri))
        {
            string headerText = $"Try to go to: {possibleURL}";
            if (headerText.Length > 36)
                headerText = headerText.Substring(0, 36) + "...";

            MenuItem urlMi = new();
            urlMi.Header = headerText;
            urlMi.Click += (sender, e) =>
            {
                Process.Start(new ProcessStartInfo(possibleURL) { UseShellExecute = true });
            };
            PassedTextControl.ContextMenu?.Items.Insert(0, new Separator());
            PassedTextControl.ContextMenu?.Items.Insert(0, urlMi);
        }

    }

    private void RemoveDuplicateLines_Click(object sender, RoutedEventArgs e)
    {
        PassedTextControl.Text = PassedTextControl.Text.RemoveDuplicateLines();
    }

    private void UpdateLineAndColumnText()
    {
        if (PassedTextControl.SelectionLength < 1)
        {
            int lineNumber = PassedTextControl.GetLineIndexFromCharacterIndex(PassedTextControl.CaretIndex);
            int columnNumber = PassedTextControl.CaretIndex - PassedTextControl.GetCharacterIndexFromLineIndex(lineNumber);

            BottomBarText.Text = $"Ln {lineNumber + 1}, Col {columnNumber}";
        }
        else
        {
            int selectionStartIndex = PassedTextControl.SelectionStart;
            int selectionStopIndex = PassedTextControl.SelectionStart + PassedTextControl.SelectionLength;

            int selStartLine = PassedTextControl.GetLineIndexFromCharacterIndex(selectionStartIndex);
            int selStartCol = selectionStartIndex - PassedTextControl.GetCharacterIndexFromLineIndex(selStartLine);
            int selStopLine = PassedTextControl.GetLineIndexFromCharacterIndex(selectionStopIndex); ;
            int selStopCol = selectionStopIndex - PassedTextControl.GetCharacterIndexFromLineIndex(selStopLine); ;
            int selLength = PassedTextControl.SelectionLength;
            int numbOfSelectedLines = selStopLine - selStartLine;

            if (numbOfSelectedLines > 0)
            {
                BottomBarText.Text = $"Ln {selStartLine + 1}:{selStopLine + 1}, Col {selStartCol}:{selStopCol}, Len {selLength}, Lines {numbOfSelectedLines + 1}";
            }
            else
            {
                BottomBarText.Text = $"Ln {selStartLine + 1}, Col {selStartCol}:{selStopCol}, Len {selLength}";
            }
        }
    }

    private void PassedTextControl_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateLineAndColumnText();
    }

    private void PassedTextControl_SelectionChanged(object sender, RoutedEventArgs e)
    {
        UpdateLineAndColumnText();
    }

    private void ListFilesMenuItem_Click(object sender, RoutedEventArgs e)
    {
        FolderBrowserDialog folderBrowserDialog1 = new FolderBrowserDialog();
        DialogResult result = folderBrowserDialog1.ShowDialog();

        if (result is System.Windows.Forms.DialogResult.OK)
        {
            string chosenFolderPath = folderBrowserDialog1.SelectedPath;
            try
            {
                IEnumerable<String> files = Directory.EnumerateFiles(chosenFolderPath);
                IEnumerable<String> folders = Directory.EnumerateDirectories(chosenFolderPath);
                StringBuilder listOfNames = new StringBuilder();
                listOfNames.Append(chosenFolderPath).Append(Environment.NewLine).Append(Environment.NewLine);
                foreach (string folder in folders)
                {
                    listOfNames.Append($"{folder.AsSpan(1 + chosenFolderPath.Length, (folder.Length - 1) - chosenFolderPath.Length)}{Environment.NewLine}");
                }
                foreach (string file in files)
                {
                    listOfNames.Append($"{file.AsSpan(1 + chosenFolderPath.Length, (file.Length - 1) - chosenFolderPath.Length)}{Environment.NewLine}");
                }

                PassedTextControl.AppendText(listOfNames.ToString());
            }
            catch (System.Exception ex)
            {
                PassedTextControl.AppendText($"Failed: {ex.Message}{Environment.NewLine}");
            }
        }
    }

    private async void ReadFolderOfImages_Click(object sender, RoutedEventArgs e)
    {
        FolderBrowserDialog folderBrowserDialog = new();
        DialogResult result = folderBrowserDialog.ShowDialog();

        if (result is not System.Windows.Forms.DialogResult.OK)
            return;

        string chosenFolderPath = folderBrowserDialog.SelectedPath;

        MessageBoxResult resultRecursive = MessageBox.Show("Would you like to OCR images in every sub-folder?", "OCR Options", MessageBoxButton.YesNoCancel);

        if (resultRecursive == MessageBoxResult.Cancel)
            return;

        bool isRecursive = false;
        if (resultRecursive == MessageBoxResult.Yes)
            isRecursive = true;

        if (Directory.Exists(chosenFolderPath))
            await OcrAllImagesInFolder(chosenFolderPath, false, isRecursive);
    }

    private async void ReadFolderOfImagesWriteTxtFiles_Click(object sender, RoutedEventArgs e)
    {
        FolderBrowserDialog folderBrowserDialog = new();
        DialogResult result = folderBrowserDialog.ShowDialog();

        if (result is not System.Windows.Forms.DialogResult.OK)
            return;

        string chosenFolderPath = folderBrowserDialog.SelectedPath;

        MessageBoxResult resultRecursive = MessageBox.Show("Would you like to make .txt files with the OCR result for all images in every sub-folder?", "OCR Options", MessageBoxButton.YesNoCancel);

        if (resultRecursive == MessageBoxResult.Cancel)
            return;

        bool isRecursive = false;
        if (resultRecursive == MessageBoxResult.Yes)
            isRecursive = true;

        if (Directory.Exists(chosenFolderPath))
            await OcrAllImagesInFolder(chosenFolderPath, true, isRecursive);
    }

    CancellationTokenSource? cancellationTokenForDirOCR;

    public async Task OcrAllImagesInFolder(string folderPath, bool writeToTextFiles, bool recrusive)
    {
        IEnumerable<String>? files = null;

        SearchOption searchOption = SearchOption.TopDirectoryOnly;
        if (recrusive)
            searchOption = SearchOption.AllDirectories;

        try
        {
            files = Directory.GetFiles(folderPath, "*.*", searchOption);
        }
        catch (System.Exception ex)
        {
            PassedTextControl.AppendText($"Failed to read directory: {ex.Message}{Environment.NewLine}");
        }

        if (files is null)
            return;

        List<string> imageFiles = files.Where(x => imageExtensions.Contains(Path.GetExtension(x).ToLower())).ToList();

        if (imageFiles.Count == 0)
        {
            PassedTextControl.AppendText($"{folderPath} contains no images");
            return;
        }

        PassedTextControl.AppendText(folderPath);
        PassedTextControl.AppendText(Environment.NewLine);
        PassedTextControl.AppendText($"{imageFiles.Count} images found");
        PassedTextControl.AppendText(Environment.NewLine);
        PassedTextControl.AppendText("Press Escape to cancel");
        PassedTextControl.AppendText(Environment.NewLine);
        PassedTextControl.AppendText(Environment.NewLine);

        Language? selectedLanguage = ImageMethods.GetOCRLanguage();
        cancellationTokenForDirOCR = new();
        CancellationToken token = cancellationTokenForDirOCR.Token;

        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = 6,
            CancellationToken = cancellationTokenForDirOCR.Token
        };

        Stopwatch stopwatch = new();
        stopwatch.Start();
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        List<AsyncOcrFileResult> ocrFileResults = new();
        foreach (string path in imageFiles)
        {
            AsyncOcrFileResult ocrFileResult = new(path);
            ocrFileResults.Add(ocrFileResult);
        }

        try
        {
            await Parallel.ForEachAsync(ocrFileResults, parallelOptions, async (ocrFile, ct) =>
            {
                ct.ThrowIfCancellationRequested();

                ocrFile.OcrResult = await OcrFile(ocrFile.FilePath, selectedLanguage, writeToTextFiles);

                // to get the TextBox to update whenever OCR Finishes:
                if (!writeToTextFiles)
                {
                    await System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
                    {
                        PassedTextControl.AppendText(ocrFile.OcrResult);
                        PassedTextControl.ScrollToEnd();
                    });
                }
            });

            PassedTextControl.AppendText(Environment.NewLine);
            PassedTextControl.AppendText($"----- COMPLETED OCR OF {imageFiles.Count} images");
        }
        catch (OperationCanceledException)
        {
            PassedTextControl.AppendText(Environment.NewLine);
            int countCompleted = ocrFileResults.Where(r => r.OcrResult is not null).Count();
            PassedTextControl.AppendText($"----- CANCELLED OCR OF {ocrFileResults.Count - countCompleted}, Completed {countCompleted} images");
        }
        finally
        {
            cancellationTokenForDirOCR.Dispose();
        }

        Mouse.OverrideCursor = null;
        stopwatch.Stop();

        PassedTextControl.AppendText(Environment.NewLine);
        PassedTextControl.AppendText($"----- from {folderPath}");
        PassedTextControl.AppendText(Environment.NewLine);
        PassedTextControl.AppendText($"----- and took {stopwatch.Elapsed.ToString("c")}");
        PassedTextControl.ScrollToEnd();

        GC.Collect();
        cancellationTokenForDirOCR = null;
    }

    private static async Task<string> OcrFile(string path, Language? selectedLanguage, bool writeResultToTextFile)
    {
        StringBuilder returnString = new();
        returnString.AppendLine(Path.GetFileName(path));
        try
        {
            string ocrdText = await ImageMethods.OcrAbsoluteFilePath(path);

            if (!string.IsNullOrWhiteSpace(ocrdText))
            {
                returnString.AppendLine(ocrdText);

                if (writeResultToTextFile && Path.GetDirectoryName(path) is string dir)
                {
                    using StreamWriter outputFile = new StreamWriter(Path.Combine(dir, $"{Path.GetFileNameWithoutExtension(path)}.txt"));
                    outputFile.WriteLine(ocrdText);
                }
            }
        }
        catch (System.Exception ex)
        {
            returnString.AppendLine($"Failed to read {path}: {ex.Message}{Environment.NewLine}");
        }

        return returnString.ToString();
    }

    private async void FSGDelayMenuItem_Click(object sender, RoutedEventArgs e)
    {
        await Task.Delay(2000);
        WindowUtilities.LaunchFullScreenGrab(true, true, PassedTextControl);
    }

    private void AddRemoveAtMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddOrRemoveWindow aorw = new();
        aorw.Owner = this;
        aorw.SelectedTextFromEditTextWindow = PassedTextControl.SelectedText;
        aorw.ShowDialog();
    }

    private void LaunchQuickSimpleLookup(object sender, RoutedEventArgs e)
    {
        QuickSimpleLookup qsl = new()
        {
            DestinationTextBox = PassedTextControl
        };
        qsl.Show();
    }

    public void RemoveCharsFromEachLine(int numberOfChars, SpotInLine spotInLine)
    {
        string[] splitString = PassedTextControl.Text.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);

        StringBuilder sb = new();
        foreach (string line in splitString)
        {
            int lineLength = line.Length;
            if (lineLength <= numberOfChars)
            {
                sb.AppendLine();
                continue;
            }

            switch (spotInLine)
            {
                case SpotInLine.Beginning:
                    sb.AppendLine(line.Substring(numberOfChars));
                    break;
                case SpotInLine.End:
                    sb.AppendLine(line.Substring(0, lineLength - numberOfChars));
                    break;
                default:
                    break;
            }
        }

        PassedTextControl.Text = sb.ToString();
    }

    public void AddCharsToEachLine(string stringToAdd, SpotInLine spotInLine)
    {
        string[] splitString = PassedTextControl.Text.Split(new string[] { System.Environment.NewLine }, StringSplitOptions.None);

        if (splitString.Length > 1)
            if (splitString.LastOrDefault() == "")
                Array.Resize(ref splitString, splitString.Length - 1);

        StringBuilder sb = new();
        foreach (string line in splitString)
        {
            switch (spotInLine)
            {
                case SpotInLine.Beginning:
                    sb.AppendLine(stringToAdd + line);
                    break;
                case SpotInLine.End:
                    sb.AppendLine(line + stringToAdd);
                    break;
                default:
                    break;
            }
        }

        PassedTextControl.Text = sb.ToString().Trim();
    }

    private void ETWindow_Drop(object sender, System.Windows.DragEventArgs e)
    {
        // Mark the event as handled, so TextBox's native Drop handler is not called.

        if (e.Data.GetDataPresent("Text"))
            return;

        e.Handled = true;
        Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop, true))
        {
            var fileNames = e.Data.GetData(System.Windows.DataFormats.FileDrop, true) as string[];
            // Check for a single file or folder.
            if (fileNames?.Length is 1)
            {
                // Check for a file (a directory will return false).
                if (File.Exists(fileNames[0]))
                    OpenThisPath(fileNames[0], false);
            }
            else if (fileNames?.Length > 1)
            {
                foreach (string possibleFilePath in fileNames)
                {
                    if (File.Exists(possibleFilePath))
                        OpenThisPath(possibleFilePath, true);
                }
            }
        }
        Mouse.OverrideCursor = null;
    }

    private void ETWindow_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        // If dropping raw text onto the ETW let the default drag/drop events occur
        bool isText = e.Data.GetDataPresent("Text");

        if (isText)
        {
            e.Handled = false;
            return;
        }

        // After here we will now allow the dropping of "non-text" content
        e.Effects = System.Windows.DragDropEffects.Copy;
        e.Handled = true;
    }
}
