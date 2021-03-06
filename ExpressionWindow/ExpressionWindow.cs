﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using System.ComponentModel;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Windows.Shell;
using System.Reflection;
using System.Windows.Media.Effects;

namespace ThemedWindows
{
    /// <summary>
    /// Logique d'interaction pour MainWindow.xaml
    /// </summary>
    public class ExpressionWindow : Window, INotifyPropertyChanged
    {
        static bool FrameLoaded = false;
        const int TITLE_BAR_HEIGHT = 24;
        const int RESIZE_HANDLE_SIZE = 6;
        const int CHROME_BUTTON_TOP_MARGIN = -1;

        public event PropertyChangedEventHandler PropertyChanged;

        const ThemeColors DEFAULT_COLOR = ThemeColors.Blue;
        public enum ThemeColors { Green, Blue, Yellow, Red, Orange, Purple, Pink, Grey }
        private const int X_BUTTON_NORMAL_WIDTH = 48;
        private const int X_BUTTON_MAXIMIZED_WIDTH = 53;
        private Thickness X_BUTTON_NORMAL_MARGIN = new Thickness(0, CHROME_BUTTON_TOP_MARGIN, 5, 0);
        private Thickness X_BUTTON_MAXIMIZED_MARGIN = new Thickness(0, CHROME_BUTTON_TOP_MARGIN, 0, 0);
        private Rect restoreLocation;

        private Version win8version = new Version(6, 2, 9200, 0);
        private bool IsWin8OrHigher { get { return Environment.OSVersion.Platform == PlatformID.Win32NT && Environment.OSVersion.Version >= win8version; } }

        Brush TitleEnabledBackground;
        Brush TitleDisabledBackground;

        WindowChrome Chrome;

        static ResourceDictionary CurrentTheme = new ResourceDictionary();

        static ExpressionWindow()
        {
            if (Application.Current.Resources is DesignTimeResourceDictionary)
            {
                Application.Current.Resources = null;
            }
            else
            {
                foreach (var dic in Application.Current.Resources.MergedDictionaries)
                    if (dic is DesignTimeResourceDictionary)
                    {
                        Application.Current.Resources.MergedDictionaries.Remove(dic);
                        break;
                    }
            }
            Application.Current.Resources.MergedDictionaries.Add(CurrentTheme);
        }

        ThemeColors themeColor;
        public ThemeColors ThemeColor
        {
            get { return themeColor; }
            set
            {
                themeColor = value;

                string ColorS = Enum.GetName(typeof(ThemeColors), value);

                CurrentTheme.Source = new Uri("pack://application:,,,/ExpressionWindow;component/Themes/" + ColorS + "Colors.xaml");

                Window_Button_Close.Style = (Style)this.FindResource("Window_Button_Close");
                Window_Button_Maximize.Style = (Style)this.FindResource("Window_Button_Maximize");
                Window_Button_Minimize.Style = (Style)this.FindResource("Window_Button_Minimize");
                Window_TitleGrid.Style = (Style)this.FindResource("Window_Frame_Title_Bar");
                Window_Border.Style = (Style)this.FindResource("Window_Frame_Border");

                OnPropertyChanged("MainColorBrush");
                OnPropertyChanged("SecondaryColorBrush");
                OnPropertyChanged("BackgroundColorBrush");
            }
        }

        private bool isColorPickerEnabled;
        public bool IsColorPickerEnabled 
        {
            get { return isColorPickerEnabled; }
            set
            {
                isColorPickerEnabled = value;
                if (value)
                {
                    Window_TitleGrid.ContextMenu = ColorPicker;
                }
                else
                {
                    Window_TitleGrid.ContextMenu = null;
                }
            }
        }

        bool isModal;
        public bool IsModal
        {
            get { return isModal; }
            set
            {
                isModal = value;
                if (!value)
                {
                    Window_TitleGrid.Visibility = System.Windows.Visibility.Visible;
                    ContentPlaceHolder.Margin = new Thickness(0, TITLE_BAR_HEIGHT, 0, 0);
                }
                else
                {
                    Window_TitleGrid.Visibility = System.Windows.Visibility.Hidden;
                    ContentPlaceHolder.Margin = new Thickness(0);
                }
            }
        }

        private bool isDarken = false;
        public bool IsDarken
        {
            get { return isDarken; }
            set
            {
                isDarken = value;
                if (value)
                {
                    DarkenOverlay.Opacity = 0.5;
                    DarkenOverlay.Visibility = System.Windows.Visibility.Visible;
                }
                else
                {
                    DarkenOverlay.Opacity = 0;
                    DarkenOverlay.Visibility = System.Windows.Visibility.Collapsed;
                }
            }
        }

        //Expose some of the maintly utilised brushes
        static public Brush MainColorBrush
        {
            get { return (Brush)Application.Current.FindResource("NormalBrush"); }
        }
        static public Brush SecondaryColorBrush
        {
            get { return (Brush)Application.Current.FindResource("NormalBorderBrush"); }
        }
        static public Brush BackgroundColorBrush
        {
            get { return (Brush)Application.Current.FindResource("BackgroundBrush"); }
        }

        int NB_COLORS = Enum.GetValues(typeof(ThemeColors)).Length;

        public static IEnumerable<ThemeColors> AvailableThemeColors
        {
            get
            {
                return Enum.GetValues(typeof(ThemeColors)).Cast<ThemeColors>();
            }
        }

        #region UI

        Border Window_Border = new Border();

        Grid Window_Grid = new Grid();

        Grid Window_TitleGrid = new Grid();
        Label Window_TitleLabel = new Label();
        Button Window_Button_Close = new Button();
        Button Window_Button_Maximize = new Button();
        Button Window_Button_Minimize = new Button();

        protected Grid Window_Content_Grid = new Grid();
        ContentControl ContentPlaceHolder = new ContentControl();

        Image TitleIcon = new Image();

        ContextMenu ColorPicker = new ContextMenu();

        Border DarkenOverlay = new Border();

        #endregion

        public ExpressionWindow()
            : base()
        {
            TitleIcon.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            TitleIcon.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            TitleIcon.Width = 16;
            TitleIcon.Height = 16;
            TitleIcon.Margin = new Thickness(8, 0, 0, 0);

            this.Icon = Imaging.CreateBitmapSourceFromHIcon(
                    System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetEntryAssembly().Location).Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions()
                    );
            TitleIcon.Source = this.Icon;

            TitleIcon.SetBinding(Image.SourceProperty, new Binding() { Path = new PropertyPath("Icon"), RelativeSource = new RelativeSource() { AncestorType = typeof(ExpressionWindow), Mode = RelativeSourceMode.FindAncestor }, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            this.MinWidth = 50;
            this.MinHeight = TITLE_BAR_HEIGHT;
            IsColorPickerEnabled = true;
            IsModal = false;
            Window_TitleLabel.SetBinding(Label.ContentProperty, new Binding() { Path = new PropertyPath("Title"), RelativeSource = new RelativeSource() { AncestorType = typeof(ExpressionWindow), Mode = RelativeSourceMode.FindAncestor }, Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });

            if (FrameLoaded)
                themeColor = ((ExpressionWindow)Application.Current.MainWindow).ThemeColor;
            else
            {
                ThemeColor = DEFAULT_COLOR;
                FrameLoaded = true;
            }

            Chrome = new WindowChrome();
            Chrome.CaptionHeight = TITLE_BAR_HEIGHT;
            Chrome.CornerRadius = new CornerRadius(0);
            Chrome.ResizeBorderThickness = new Thickness(RESIZE_HANDLE_SIZE);
            Chrome.GlassFrameThickness = new Thickness(0);
            WindowChrome.SetWindowChrome(this, Chrome);

            Initialize();

            Application.Current.Deactivated += Current_Deactivated;
            Application.Current.Activated += Current_Activated;
        }

        public virtual void Current_Activated(object sender, EventArgs e)
        {
            foreach (ExpressionWindow w in Application.Current.Windows.OfType<ExpressionWindow>())
                w.IsForeground = true;
        }

        public virtual void Current_Deactivated(object sender, EventArgs e)
        {
            foreach (ExpressionWindow w in Application.Current.Windows.OfType<ExpressionWindow>())
                w.IsForeground = false;
        }

        public bool isForeground = true;
        public bool IsForeground
        {
            get { return isForeground; }
            set
            {
                isForeground = value;
                this.Window_TitleGrid.Background = value ? TitleEnabledBackground : TitleDisabledBackground;

                Window_Button_Close.Tag = value ? "" : "Shaded";
                Window_Button_Maximize.Tag = value ? "" : "Shaded";
                Window_Button_Minimize.Tag = value ? "" : "Shaded";
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);

            if (Window.GetWindow(this).WindowState == System.Windows.WindowState.Maximized)
            {
                Chrome.ResizeBorderThickness = new Thickness(0);
                if (IsWin8OrHigher)
                {
                    Window_Border.Margin = new Thickness(4);
                }
                else
                {
                    Window_Border.Margin = new Thickness(4,6,4,4);
                }
                    
                Window_Border.BorderThickness = new Thickness(0);
                Window_Button_Maximize.Content = 2;
                Window_Button_Close.Width = X_BUTTON_MAXIMIZED_WIDTH;
                Window_Button_Close.Margin = X_BUTTON_MAXIMIZED_MARGIN;
            }
            else
            {
                Chrome.ResizeBorderThickness = new Thickness(RESIZE_HANDLE_SIZE);
                Window_Border.Margin = new Thickness(0);
                Window_Border.BorderThickness = new Thickness(1);
                Window_Button_Maximize.Content = 1;
                Window_Button_Close.Width = X_BUTTON_NORMAL_WIDTH;
                Window_Button_Close.Margin = X_BUTTON_NORMAL_MARGIN;
            }
        }

        protected override void OnContentChanged(object oldContent, object newContent)
        {
            // REQUIRED TO KEEP DESIGNER SUPPORT
            if (oldContent == Window_Border && newContent != Window_Border)
            {
                object Backup = newContent;
                newContent = Window_Border;
                ContentPlaceHolder.Content = Backup;
                this.Content = Window_Border;
            }
            else
            {
                base.OnContentChanged(oldContent, newContent);
            }
        }

        void Initialize()
        {
            Window_Border = new Border();
            this.Content = Window_Border;

            Window_Border.BorderThickness = new Thickness(1);
            Window_Border.Background = new SolidColorBrush(Color.FromRgb(56, 56, 56));
            Window_Border.ClipToBounds = true;
            Window_Border.Child = Window_Grid;

            DarkenOverlay.Background = Brushes.Black;
            DarkenOverlay.Opacity = 0;
            DarkenOverlay.Visibility = System.Windows.Visibility.Collapsed;
            DarkenOverlay.BorderThickness = new Thickness(0);
            DarkenOverlay.IsHitTestVisible = false;

            Window_Grid.Children.Add(Window_TitleGrid);
            Window_Content_Grid.Children.Add(ContentPlaceHolder);
            Window_Grid.Children.Add(Window_Content_Grid);
            Window_Grid.Children.Add(DarkenOverlay);

            #region TITLE_BAR

            Window_TitleGrid.ClipToBounds = true;
            Window_TitleGrid.Height = TITLE_BAR_HEIGHT;
            Window_TitleGrid.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            Window_TitleGrid.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            Window_TitleGrid.Children.Add(TitleIcon);
            Window_TitleGrid.Children.Add(Window_TitleLabel);
            Window_TitleGrid.Children.Add(Window_Button_Close);
            Window_TitleGrid.Children.Add(Window_Button_Maximize);
            Window_TitleGrid.Children.Add(Window_Button_Minimize);

            Window_TitleLabel.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            Window_TitleLabel.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
            Window_TitleLabel.Height = TITLE_BAR_HEIGHT - 2;
            Window_TitleLabel.VerticalContentAlignment = System.Windows.VerticalAlignment.Center;
            Window_TitleLabel.Margin = new Thickness(30, 0, 140, 0);
            Window_TitleLabel.Foreground = Brushes.White;

            Window_Button_Close.MouseEnter += Window_Button_MouseEnter;
            Window_Button_Close.MouseLeave += Window_Button_MouseLeave;
            Window_Button_Close.Click += Window_Close;
            Window_Button_Close.Content = 'r';
            Window_Button_Close.FontFamily = new System.Windows.Media.FontFamily("Webdings");
            Window_Button_Close.FontSize = 11;
            Window_Button_Close.Height = 19;
            Window_Button_Close.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            Window_Button_Close.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            Window_Button_Close.Padding = new Thickness(0, -CHROME_BUTTON_TOP_MARGIN - 1, 0, 0);
            Window_Button_Close.Foreground = Brushes.Black;
            Window_Button_Close.BorderThickness = new Thickness(1);
            Window_Button_Close.FontWeight = FontWeights.Bold;
            Window_Button_Close.Width = X_BUTTON_NORMAL_WIDTH;
            Window_Button_Close.Margin = new Thickness(0, CHROME_BUTTON_TOP_MARGIN, 5, 0);
            Window_Button_Close.Focusable = false;
            WindowChrome.SetIsHitTestVisibleInChrome(Window_Button_Close, true);

            Window_Button_Maximize.MouseEnter += Window_Button_MouseEnter;
            Window_Button_Maximize.MouseLeave += Window_Button_MouseLeave;
            Window_Button_Maximize.Click += Window_MaximizeRestore;
            Window_Button_Maximize.Content = '1';
            Window_Button_Maximize.FontFamily = new System.Windows.Media.FontFamily("Webdings");
            Window_Button_Maximize.FontSize = 11;
            Window_Button_Maximize.Height = 19;
            Window_Button_Maximize.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            Window_Button_Maximize.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            Window_Button_Maximize.Padding = new Thickness(0, -CHROME_BUTTON_TOP_MARGIN - 1, 0, 0);
            Window_Button_Maximize.Foreground = Brushes.Black;
            Window_Button_Maximize.BorderThickness = new Thickness(1);
            Window_Button_Maximize.FontWeight = FontWeights.Bold;
            Window_Button_Maximize.Width = 30;
            Window_Button_Maximize.Margin = new Thickness(0, CHROME_BUTTON_TOP_MARGIN, 52, 0);
            Window_Button_Maximize.Focusable = false;
            WindowChrome.SetIsHitTestVisibleInChrome(Window_Button_Maximize, true);

            Window_Button_Minimize.MouseEnter += Window_Button_MouseEnter;
            Window_Button_Minimize.MouseLeave += Window_Button_MouseLeave;
            Window_Button_Minimize.Click += Window_Minimize;
            Window_Button_Minimize.Content = '0';
            Window_Button_Minimize.FontFamily = new System.Windows.Media.FontFamily("Webdings");
            Window_Button_Minimize.FontSize = 11;
            Window_Button_Minimize.Height = 19;
            Window_Button_Minimize.HorizontalAlignment = System.Windows.HorizontalAlignment.Right;
            Window_Button_Minimize.VerticalAlignment = System.Windows.VerticalAlignment.Top;
            Window_Button_Minimize.Padding = new Thickness(0, -CHROME_BUTTON_TOP_MARGIN - 1, 0, 0);
            Window_Button_Minimize.Foreground = Brushes.Black;
            Window_Button_Minimize.BorderThickness = new Thickness(1);
            Window_Button_Minimize.FontWeight = FontWeights.Bold;
            Window_Button_Minimize.Width = 30;
            Window_Button_Minimize.Margin = new Thickness(0, CHROME_BUTTON_TOP_MARGIN, 81, 0);
            Window_Button_Minimize.Focusable = false;
            WindowChrome.SetIsHitTestVisibleInChrome(Window_Button_Minimize, true);

            #endregion

            #region COLOR_PICKER
            foreach (ThemeColors t in AvailableThemeColors)
            {
                MenuItem NewItem = new MenuItem();
                NewItem.Header = t;
                NewItem.Click += (o, e) =>
                {
                    ThemeColor = (ThemeColors)((MenuItem)e.Source).Header;
                };
                ColorPicker.Items.Add(NewItem);
            }
            //Window_TitleGrid.ContextMenu = ColorPicker;
            #endregion

            Window_Button_Close.Style = (Style)this.FindResource("Window_Button_Close");
            Window_Button_Maximize.Style = (Style)this.FindResource("Window_Button_Maximize");
            Window_Button_Minimize.Style = (Style)this.FindResource("Window_Button_Minimize");
            Window_TitleGrid.Style = (Style)this.FindResource("Window_Frame_Title_Bar");
            Window_Border.Style = (Style)this.FindResource("Window_Frame_Border");

            TitleEnabledBackground = Window_TitleGrid.Background.CloneCurrentValue();
            TitleDisabledBackground = TitleEnabledBackground.CloneCurrentValue();
            TitleDisabledBackground.Opacity = 0.4;
            TitleEnabledBackground.Freeze();
            TitleDisabledBackground.Freeze();
        }

        private void Window_Close(object sender, RoutedEventArgs e)
        {
            SystemCommands.CloseWindow(Window.GetWindow(this));
        }

        private void Window_Minimize(object sender, RoutedEventArgs e)
        {
            this.Focus();
            this.WindowState = System.Windows.WindowState.Minimized;
        }

        private void Restore()
        {
            Height = restoreLocation.Height;
            Width = restoreLocation.Width;
            Left = restoreLocation.X;
            Top = restoreLocation.Y;
        }

        private void Window_MaximizeRestore(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this).WindowState == System.Windows.WindowState.Maximized)
                SystemCommands.RestoreWindow(this);
            else
                SystemCommands.MaximizeWindow(this);
        }

        private void Window_Button_MouseEnter(object sender, MouseEventArgs e)
        {
            ((Button)sender).Foreground = new SolidColorBrush(Colors.White);
        }

        private void Window_Button_MouseLeave(object sender, MouseEventArgs e)
        {
            ((Button)sender).Foreground = new SolidColorBrush(Colors.Black);
        }

        private void Form_Close(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        protected void OnPropertyChanged(string name)
        {
            PropertyChangedEventHandler handler = PropertyChanged;
            if (handler != null)
            {
                handler(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}