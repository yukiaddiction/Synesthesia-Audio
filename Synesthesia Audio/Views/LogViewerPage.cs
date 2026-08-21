using CommunityToolkit.Maui.Markup;
using Microsoft.Extensions.Logging;
using Synesthesia_Audio.ViewModels;
using Synesthesia_Audio.Styles;
using static CommunityToolkit.Maui.Markup.GridRowsColumns;

namespace Synesthesia_Audio.Views;

public class LogViewerPage : ContentPage
{
    private readonly LogViewerViewModel _viewModel;

    public LogViewerPage(LogViewerViewModel viewModel)
    {
        BindingContext = _viewModel = viewModel;

        this.Bind(ContentPage.TitleProperty, nameof(LogViewerViewModel.Title));
        this.AppThemeBinding(ContentPage.BackgroundColorProperty, AppColors.BackgroundLight, AppColors.BackgroundDark);

        Content = new Grid
        {
            RowDefinitions = Rows.Define(Auto, Auto, Star),
            Children =
            {
                new HorizontalStackLayout
                {
                    Padding = new Thickness(16, 12, 16, 4),
                    Spacing = AppLayout.SpacingExtraLarge,
                    Children =
                    {
                        new Button { Text = "↻ Refresh", HeightRequest = AppLayout.ButtonHeightCompact, FontSize = AppFontSizes.Body }
                            .Bind(Button.CommandProperty, nameof(LogViewerViewModel.RefreshCommand))
                            .AppThemeBinding(Button.TextColorProperty, Colors.White, AppColors.TextPrimaryLight)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.PrimaryLight, AppColors.PrimaryDark),

                        new Button { Text = "📂 Open File", HeightRequest = AppLayout.ButtonHeightCompact, FontSize = AppFontSizes.Body }
                            .Bind(Button.CommandProperty, nameof(LogViewerViewModel.OpenFileCommand))
                            .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark),

                        new Button { Text = "📋 Copy", HeightRequest = AppLayout.ButtonHeightCompact, FontSize = AppFontSizes.Body }
                            .Bind(Button.CommandProperty, nameof(LogViewerViewModel.CopyCommand))
                            .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                            .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.ControlBgLight, AppColors.ControlBgDark),

                        new ActivityIndicator { HeightRequest = 24, WidthRequest = 24, Color = AppColors.PrimaryLight }
                            .Bind(ActivityIndicator.IsRunningProperty, nameof(LogViewerViewModel.IsBusy))
                            .Bind(VisualElement.IsVisibleProperty, nameof(LogViewerViewModel.IsBusy))
                    }
                }
                .Row(0)
                .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.SurfaceLight, AppColors.SurfaceDark),

                new Button { Margin = new Thickness(16, 6, 16, 8), HeightRequest = 34, FontSize = AppFontSizes.SmallText }
                    .Bind(Button.TextProperty, nameof(LogViewerViewModel.ModeLabel))
                    .Bind(Button.CommandProperty, nameof(LogViewerViewModel.ToggleModeCommand))
                    .AppThemeBinding(Button.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                    .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.StrokeAltLight, AppColors.SurfaceDark)
                    .Row(1),

                new ScrollView
                {
                    Padding = new Thickness(16, 0, 16, 16),
                    Content = new Label { FontFamily = AppFonts.Regular, FontSize = AppFontSizes.Caption, Padding = new Thickness(14), LineBreakMode = LineBreakMode.WordWrap }
                        .Bind(Label.TextProperty, nameof(LogViewerViewModel.LogContent))
                        .AppThemeBinding(Label.TextColorProperty, AppColors.TextPrimaryLight, AppColors.TextPrimaryDark)
                        .AppThemeBinding(VisualElement.BackgroundColorProperty, AppColors.SurfaceLight, AppColors.SurfaceDark)
                }
                .Row(2)
            }
        };
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
            Services.Logging.AppLog.Get<LogViewerPage>().LogWarning(ex, "LogViewerPage OnAppearing error");
        }
    }
}
