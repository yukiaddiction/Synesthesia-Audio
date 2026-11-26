using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.Services.Logging;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views.Components;

/// <summary>
/// The glass page frame. Above <see cref="AppBreakpoints.Wide"/> it adds a persistent sidebar and
/// drops the menu button; below it behaves exactly like the compact phone layout.
/// </summary>
/// <remarks>
/// One composition reflows rather than two layouts being built: the sidebar column animates
/// between 0 and its full width, and pages subscribe to <see cref="LayoutModeChanged"/> to toggle
/// optional panes. Pages must also call <c>Shell.SetNavBarIsVisible(this, false)</c>.
/// </remarks>
public sealed class GlassScaffold : Grid
{
    private readonly ColumnDefinition _sidebarColumn;
    private readonly GlassSidebar _sidebar;
    private readonly Button? _menuButton;
    private readonly Grid _bodyHost;
    private readonly Grid _overlayHost;
    private readonly HorizontalStackLayout _actionBar;

    private bool _isWide;
    private bool _modeInitialized;

    /// <summary>Raised when the window crosses the wide breakpoint.</summary>
    public event EventHandler<bool>? LayoutModeChanged;

    public GlassScaffold(string title, string? subtitle = null, bool back = false, bool menu = true)
    {
        RowDefinitions = Rows.Define(Star);
        ColumnDefinitions = Columns.Define(Star);
        BackgroundColor = Colors.Transparent;

        TitleLabel = new Label
        {
            Text = title,
            FontSize = AppFontSizes.SectionTitle,
            FontAttributes = AppFonts.Bold,
            LineBreakMode = LineBreakMode.TailTruncation,
            VerticalOptions = LayoutOptions.Center
        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark);

        SubtitleLabel = new Label
        {
            Text = subtitle ?? string.Empty,
            FontSize = AppFontSizes.Caption,
            IsVisible = !string.IsNullOrEmpty(subtitle),
            LineBreakMode = LineBreakMode.TailTruncation
        }.AppThemeBinding(Label.TextColorProperty, AppColors.TextSecondaryLight, AppColors.TextSecondaryDark);

        _actionBar = new HorizontalStackLayout
        {
            Spacing = AppLayout.SpacingLarge,
            VerticalOptions = LayoutOptions.Center
        };

        Backdrop = new GlassBackdrop();
        Backdrop.AppThemeBinding(GlassBackdrop.IsDarkProperty, false, true);

        _bodyHost = new Grid();
        _overlayHost = new Grid { InputTransparent = true, CascadeInputTransparent = false };

        var main = new Grid { RowDefinitions = Rows.Define(Auto, Star) };
        var appBar = BuildBar(back, menu, out _menuButton);

        main.Children.Add(appBar);
        appBar.Row(0);
        main.Children.Add(_bodyHost);
        _bodyHost.Row(1);
        main.Children.Add(_overlayHost);
        _overlayHost.Row(1);

        _sidebar = new GlassSidebar { IsVisible = false };        _sidebarColumn = new ColumnDefinition(new GridLength(0));

        var shell = new Grid
        {
            ColumnDefinitions = { _sidebarColumn, new ColumnDefinition(GridLength.Star) }
        };
        shell.Children.Add(_sidebar);
        _sidebar.Column(0);
        shell.Children.Add(main);
        main.Column(1);

        Children.Add(Backdrop);
        Children.Add(shell);

        SizeChanged += OnSizeChanged;
    }

    /// <summary>Primary page heading shown in the app bar.</summary>
    public Label TitleLabel { get; }

    /// <summary>Supporting caption under the heading.</summary>
    public Label SubtitleLabel { get; }

    /// <summary>The theme-bound aurora background.</summary>
    public GlassBackdrop Backdrop { get; }

    /// <summary>The desktop navigation rail (hidden while compact).</summary>
    public GlassSidebar Sidebar => _sidebar;

    /// <summary>True while the window is wide enough for the desktop composition.</summary>
    public bool IsWide => _isWide;

    /// <summary>Right-aligned app bar actions.</summary>
    public IList<IView> Actions => _actionBar.Children;

    /// <summary>Replaces the page body.</summary>
    public void SetBody(View body)
    {
        _bodyHost.Children.Clear();
        _bodyHost.Children.Add(body);
    }

    /// <summary>Adds a floating element on top of the body (player, toast).</summary>
    public void AddOverlay(View overlay) => _overlayHost.Children.Add(overlay);

    private void OnSizeChanged(object? sender, EventArgs e) => ApplyWidth(Width);

    private void ApplyWidth(double width)
    {
        if (width <= 0)
            return;

        var wide = AppBreakpoints.IsWide(width);
        if (_modeInitialized && wide == _isWide)
            return;

        _isWide = wide;
        _modeInitialized = true;

        _sidebarColumn.Width = new GridLength(wide ? AppBreakpoints.SidebarWidth : 0);
        _sidebar.IsVisible = wide;
        if (_menuButton != null)
            _menuButton.IsVisible = !wide;

        SyncFlyoutBehavior(wide);
        LayoutModeChanged?.Invoke(this, wide);
    }

    /// <summary>
    /// Shell's own flyout is redundant once the sidebar is visible, and disabling it also removes
    /// the stray pane toggle Shell draws into the Windows title bar.
    /// </summary>
    private static void SyncFlyoutBehavior(bool wide)
    {
        try
        {
            if (Shell.Current is { } shell)
                shell.FlyoutBehavior = wide ? FlyoutBehavior.Disabled : FlyoutBehavior.Flyout;
        }
        catch (Exception ex)
        {
            AppLog.Get<GlassScaffold>().LogWarning(ex, "Could not switch the shell flyout behaviour");
        }
    }

    private Border BuildBar(bool back, bool menu, out Button? menuButton)
    {
        var titleStack = new VerticalStackLayout
        {
            Spacing = 0,
            VerticalOptions = LayoutOptions.Center,
            Children = { TitleLabel, SubtitleLabel }
        };

        var barGrid = new Grid
        {
            ColumnDefinitions = Columns.Define(Auto, Star, Auto),
            ColumnSpacing = AppLayout.SpacingMedium
        };

        menuButton = null;
        if (back)
        {
            barGrid.Children.Add(CreateBackButton().Column(0));
        }
        else if (menu)
        {
            menuButton = CreateMenuButton();
            barGrid.Children.Add(menuButton.Column(0));
        }

        barGrid.Children.Add(titleStack.Column(1));
        barGrid.Children.Add(_actionBar.Column(2));

        return new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassBarRadius },
            Margin = AppLayout.GlassBarMargin,
            Padding = AppLayout.GlassBarPadding,
            Content = barGrid
        }.Glass(strong: true, elevated: true);
    }

    private static Button CreateBackButton()
    {
        var button = GlassTheme.IconButton("\u2190");
        button.AutomationId = "GlassBackButton";
        ToolTipProperties.SetText(button, "Back");
        SemanticProperties.SetDescription(button, "Back");
        button.Clicked += async (_, _) => await GoBackAsync();
        return button;
    }

    private static Button CreateMenuButton()
    {
        var button = GlassTheme.IconButton("\u2630");
        button.AutomationId = "GlassMenuButton";
        ToolTipProperties.SetText(button, "Open navigation menu");
        SemanticProperties.SetDescription(button, "Open navigation menu");
        button.Clicked += (_, _) =>
        {
            try
            {
                if (Shell.Current is { } shell)
                    shell.FlyoutIsPresented = true;
            }
            catch (Exception ex)
            {
                AppLog.Get<GlassScaffold>().LogWarning(ex, "Opening the flyout failed");
            }
        };
        return button;
    }

    private static async Task GoBackAsync()
    {
        try
        {
            var navigation = Shell.Current?.Navigation;
            if (navigation == null) return;

            // A page presented modally (the edit sheet, the folder picker) lives on the modal stack. Popping the
            // shell stack instead navigated the view *behind* the open sheet and left it open (UX-04).
            if (navigation.ModalStack.Count > 0)
            {
                await navigation.PopModalAsync();
                return;
            }

            if (navigation.NavigationStack.Count > 1)
            {
                await navigation.PopAsync();
                return;
            }

            if (Shell.Current is { } shell)
                await shell.GoToAsync("//Library");
        }
        catch (Exception ex)
        {
            AppLog.Get<GlassScaffold>().LogWarning(ex, "Glass back navigation failed");
        }
    }
}
