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
/// Page frame that reflows one composition instead of building two layouts: past the wide breakpoint the
/// sidebar column opens and pages toggle their own panes via <see cref="LayoutModeChanged"/>.
/// Host pages must hide Shell's nav bar themselves, since the scaffold draws its own app bar.
/// </summary>
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

    public event EventHandler<bool>? LayoutModeChanged;

    private readonly ILogger _logger;

    public GlassScaffold(string title, string? subtitle = null, bool back = false, bool menu = true, ILogger? logger = null)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
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

        _sidebar = new GlassSidebar(logger: _logger) { IsVisible = false };        _sidebarColumn = new ColumnDefinition(new GridLength(0));

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

    public Label TitleLabel { get; }

    public Label SubtitleLabel { get; }

    public GlassBackdrop Backdrop { get; }

    public GlassSidebar Sidebar => _sidebar;

    public bool IsWide => _isWide;

    public IList<IView> Actions => _actionBar.Children;

    public void SetBody(View body)
    {
        _bodyHost.Children.Clear();
        _bodyHost.Children.Add(body);
    }

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

        SyncFlyoutBehavior(wide, _logger);
        LayoutModeChanged?.Invoke(this, wide);
    }

    /// <summary>
    /// Disables Shell's flyout while the sidebar shows: it's redundant, and this also removes the stray pane
    /// toggle Shell draws in the Windows title bar.
    /// </summary>
    private static void SyncFlyoutBehavior(bool wide, ILogger logger)
    {
        try
        {
            if (Shell.Current is { } shell)
                shell.FlyoutBehavior = wide ? FlyoutBehavior.Disabled : FlyoutBehavior.Flyout;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not switch the shell flyout behaviour");
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

    private Button CreateBackButton()
    {
        var button = GlassTheme.IconButton("\u2190");
        button.AutomationId = "GlassBackButton";
        ToolTipProperties.SetText(button, "Back");
        SemanticProperties.SetDescription(button, "Back");
        button.Clicked += async (_, _) => await GoBackAsync(_logger);
        return button;
    }

    private Button CreateMenuButton()
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
                _logger.LogWarning(ex, "Opening the flyout failed");
            }
        };
        return button;
    }

    private static async Task GoBackAsync(ILogger logger)
    {
        try
        {
            var navigation = Shell.Current?.Navigation;
            if (navigation == null) return;

            // Modal pages must pop the modal stack; popping Shell's would navigate the page behind the open sheet.
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
            logger.LogWarning(ex, "Glass back navigation failed");
        }
    }
}
