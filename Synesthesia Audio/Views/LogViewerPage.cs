using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Controls.Shapes;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Styles;
using Synesthesia_Audio.Views.Components;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views;

public class LogViewerPage : ContentPage
{
    private readonly LogViewerViewModel _viewModel;

    private readonly ILogger<LogViewerPage> _logger;

    public LogViewerPage(LogViewerViewModel viewModel, ILogger<LogViewerPage> logger)
    {
        _logger = logger;
        BindingContext = _viewModel = viewModel;

        this.Bind(ContentPage.TitleProperty, nameof(LogViewerViewModel.Title));
        Shell.SetNavBarIsVisible(this, false);

        var scaffold = new GlassScaffold("Application Logs", "Tail or full diagnostics", back: true);

        scaffold.Actions.Add(GlassTheme.IconButton("\u21BB", AppFontSizes.ButtonLarge)
            .Bind(Button.CommandProperty, nameof(LogViewerViewModel.RefreshCommand))
            .Invoke(b => b.AutomationId = "RefreshLogsButton")
            .Invoke(b => SemanticProperties.SetDescription(b, "Refresh logs")));

        scaffold.Actions.Add(GlassTheme.IconButton("\uD83D\uDCC2", AppFontSizes.SmallText)
            .Bind(Button.CommandProperty, nameof(LogViewerViewModel.OpenFileCommand))
            .Invoke(b => b.AutomationId = "OpenLogFileButton")
            .Invoke(b => SemanticProperties.SetDescription(b, "Open log file")));

        scaffold.Actions.Add(GlassTheme.IconButton("\uD83D\uDCCB", AppFontSizes.SmallText)
            .Bind(Button.CommandProperty, nameof(LogViewerViewModel.CopyCommand))
            .Invoke(b => b.AutomationId = "CopyLogsButton")
            .Invoke(b => SemanticProperties.SetDescription(b, "Copy logs")));

        var busy = new ActivityIndicator { HeightRequest = 22, WidthRequest = 22, VerticalOptions = LayoutOptions.Center }
            .AppThemeBinding(ActivityIndicator.ColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark)
            .Bind(ActivityIndicator.IsRunningProperty, nameof(LogViewerViewModel.IsBusy))
            .Bind(VisualElement.IsVisibleProperty, nameof(LogViewerViewModel.IsBusy));

        var modeToggle = GlassTheme.PillButton(string.Empty, AppFontSizes.SmallText, height: 34)
            .Bind(Button.TextProperty, nameof(LogViewerViewModel.ModeLabel))
            .Bind(Button.CommandProperty, nameof(LogViewerViewModel.ToggleModeCommand))
            .Invoke(b => b.HorizontalOptions = LayoutOptions.Start);

        var logPanel = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = AppLayout.GlassCardRadius },
            Padding = new Thickness(14),
            HorizontalOptions = LayoutOptions.Center,
            MaximumWidthRequest = AppBreakpoints.ReaderMaxWidth,
            // Virtualised so "show all" doesn't lay out every log line at once.
            Content = new CollectionView
            {
                ItemTemplate = new DataTemplate(() =>
                    new Label
                    {
                        FontFamily = AppFonts.Regular,
                        FontSize = AppFontSizes.Caption,
                        LineBreakMode = LineBreakMode.WordWrap
                    }
                    .Bind(Label.TextProperty, ".")
                    // Light text in both themes: the GlassDeep panel behind it is dark in both.
                    .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryDark, AppColors.TextPrimaryDark))
            }
            .Bind(ItemsView.ItemsSourceProperty, nameof(LogViewerViewModel.LogLines))
        }.GlassDeep();

        var body = new Grid
        {
            RowDefinitions = Rows.Define(Auto, Star),
            Padding = new Thickness(16, 0, 16, 16),
            RowSpacing = AppLayout.SpacingLarge,
            Children =
            {
                new HorizontalStackLayout
                {
                    Spacing = AppLayout.SpacingLarge,
                    Children = { modeToggle, busy }
                }.Row(0),

                logPanel.Row(1)
            }
        };

        scaffold.SetBody(body);
        Content = scaffold;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try
        {
            await _viewModel.LoadTailAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LogViewerPage OnAppearing error");
        }
    }
}
