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
/// The glass page frame: aurora backdrop, floating glass app bar, a body slot, and an overlay
/// slot for floating chrome such as the player bar.
/// </summary>
/// <remarks>
/// The app bar replaces the Shell navigation bar, so pages using this scaffold must call
/// <c>Shell.SetNavBarIsVisible(this, false)</c> and rely on <c>back: true</c> for pushed routes.
/// </remarks>
public sealed class GlassScaffold : Grid
{
    private readonly Grid _bodyHost;
    private readonly Grid _overlayHost;
    private readonly HorizontalStackLayout _actionBar;

    public GlassScaffold(string title, string? subtitle = null, bool back = false, bool menu = true)
    {
        RowDefinitions = Rows.Define(Auto, Star);
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

        _bodyHost = new Grid();
        _overlayHost = new Grid { InputTransparent = true, CascadeInputTransparent = false };

        Backdrop = new GlassBackdrop();
        Backdrop.AppThemeBinding(GlassBackdrop.IsDarkProperty, false, true);

        var bar = BuildBar(back, menu);

        Children.Add(Backdrop);
        Children.Add(bar);
        Children.Add(_bodyHost);
        Children.Add(_overlayHost);

        Backdrop.Row(0).RowSpan(2);
        bar.Row(0);
        _bodyHost.Row(1);
        _overlayHost.Row(1);
    }

    /// <summary>Primary page heading shown in the app bar.</summary>
    public Label TitleLabel { get; }

    /// <summary>Supporting caption under the heading. Hidden until it has text.</summary>
    public Label SubtitleLabel { get; }

    /// <summary>The theme-bound aurora background.</summary>
    public GlassBackdrop Backdrop { get; }

    /// <summary>Right-aligned app bar actions.</summary>
    public IList<IView> Actions => _actionBar.Children;

    /// <summary>Replaces the page body.</summary>
    public void SetBody(View body)
    {
        _bodyHost.Children.Clear();
        _bodyHost.Children.Add(body);
    }

    /// <summary>Adds a floating element on top of the body (player bar, scan toast).</summary>
    public void AddOverlay(View overlay) => _overlayHost.Children.Add(overlay);

    private View BuildBar(bool back, bool menu)
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

        if (back)
        {
            barGrid.Children.Add(CreateBackButton().Column(0));
        }
        else if (menu)
        {
            barGrid.Children.Add(CreateMenuButton().Column(0));
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
        SemanticProperties.SetDescription(button, "Back");
        button.Clicked += async (_, _) => await GoBackAsync();
        return button;
    }

    private static Button CreateMenuButton()
    {
        var button = GlassTheme.IconButton("\u2630");
        button.AutomationId = "GlassMenuButton";
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
            if (navigation != null && navigation.NavigationStack.Count > 1)
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
