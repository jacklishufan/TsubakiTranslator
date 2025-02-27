﻿using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using TsubakiTranslator.BasicLibrary;
using TsubakiTranslator.TranslateAPILibrary;

namespace TsubakiTranslator
{
    /// <summary>
    /// TranslatedResultDisplay.xaml 的交互逻辑
    /// </summary>
    public partial class TranslatedResultDisplay : UserControl
    {
        SourceTextContent sourceTextContent;
        Dictionary<string, TranslatedData> displayTextContent;

        LinkedList<ITranslator> translators;

        private TranslateDataList results;
        public TranslateDataList Results { get => results; }

        SourceTextHandler sourceTextHandler;

        ClipboardHookHandler clipboardHookHandler;

        public bool TranslatorEnabled { get; set; } = true;

        private void Init()
        {
            SourceText.Foreground = new SolidColorBrush(App.WindowConfig.SourceTextColor);
            SourceText.FontFamily = new FontFamily(App.WindowConfig.SourceTextFontFamily);

            translators = TranslateHandler.GetSelectedTranslators(App.TranslateAPIConfig, App.OtherConfig.SourceLangIndex);

            displayTextContent = new Dictionary<string, TranslatedData>();
            foreach (ITranslator t in translators)
            {
                TranslatedResultItem resultItem = new TranslatedResultItem(t.Name, "");

                if (!App.WindowConfig.TranslatorNameVisibility)
                    resultItem.APINameTextBlock.Visibility = Visibility.Collapsed;

                resultItem.ResultTextBlock.Foreground = new SolidColorBrush(App.WindowConfig.TranslatedTextColor);
                resultItem.ResultTextBlock.FontFamily = new FontFamily(App.WindowConfig.TranslatedTextFontFamily);
                TranslateResultPanel.Children.Add(resultItem);
                displayTextContent.Add(t.Name, resultItem.TranslatedData);
            }


            sourceTextContent = new SourceTextContent();
            this.DataContext = sourceTextContent;

            if (App.OtherConfig.SaveLogEnabled)
                results = new TranslateDataList(40, App.OtherConfig.LogFolderPath);
            else
                //最多保留40条历史记录
                results = new TranslateDataList(40);
        }

        //对应注入模式
        public TranslatedResultDisplay(SourceTextHandler sourceTextHandler)
        {
            InitializeComponent();

            Init();

            this.sourceTextHandler = sourceTextHandler;

        }

        //对应剪切板翻译模式
        public TranslatedResultDisplay(ClipboardHookHandler clipboardHookHandler, SourceTextHandler sourceTextHandler)
        {
            InitializeComponent();

            Init();

            this.clipboardHookHandler = clipboardHookHandler;

            this.sourceTextHandler = sourceTextHandler;

            this.clipboardHookHandler.ClipboardUpdated += TranslteClipboardText;
        }

        //对应OCR翻译模式
        public TranslatedResultDisplay()
        {
            InitializeComponent();

            Init();

        }


        public void TranslateHookText(string text)
        {
            string sourceText = sourceTextHandler.HandleText(text);

            if (Regex.Replace(sourceText, @"\s", "").Equals(""))
                return;


            Task.Run(() => TranslateAndDisplay(sourceText));
        }

        public void TranslteClipboardText(object sender, EventArgs e)
        {
            if (!TranslatorEnabled)
                return;

            IDataObject iData = Clipboard.GetDataObject();

            if (!iData.GetDataPresent(DataFormats.Text))
                return;

            string sourceText = Clipboard.GetText();
            sourceText = Regex.Replace(sourceText, @"[\r\n\t\f]", "");
            sourceText = sourceTextHandler.HandleText(sourceText);
            Task.Run(() => TranslateAndDisplay(sourceText));
        }



        public void TranslateAndDisplay(string sourceText)
        {
            TranslateData currentResult = new TranslateData(sourceText, new Dictionary<string, string>());
            Results.AddTranslateData(currentResult);

            sourceTextContent.BindingText = currentResult.SourceText;

            foreach (var key in displayTextContent.Keys)
            {
                displayTextContent[key].TranslatedResult = "";
                currentResult.ResultText.Add(key, "");
            }


            Parallel.ForEach(translators, 
                t =>
                {
                    string result = t.Translate(currentResult.SourceText);
                    currentResult.ResultText[t.Name] = result;
                    if (sourceTextContent.BindingText == currentResult.SourceText) // handle async race condition
                    {
                        displayTextContent[t.Name].TranslatedResult = result;
                    }
                   
                });
        }

        class SourceTextContent : ObservableObject
        {

            private string text;

            public string BindingText
            {
                get => text;
                set => SetProperty(ref text, value);
            }

        }



        private void ArrowLeft_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Results.Count() == 0)
                return;
            TranslateData result = Results.GetPreviousData();
            ShowTranslateResult(result);
        }

        private void ArrowRight_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Results.Count() == 0)
                return;
            TranslateData result = Results.GetNextData();
            ShowTranslateResult(result);
        }

        private void ChevronTripleLeft_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Results.Count() == 0)
                return;
            TranslateData result = Results.GetFirstData();
            ShowTranslateResult(result);

        }

        private void ChevronTripleRight_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Results.Count() == 0)
                return;
            TranslateData result = Results.GetLastData();
            ShowTranslateResult(result);
        }

        private void ShowTranslateResult(TranslateData data)
        {
            sourceTextContent.BindingText = data.SourceText;

            foreach (ITranslator t in translators)
                displayTextContent[t.Name].TranslatedResult = data.ResultText[t.Name];

        }

    }
}
