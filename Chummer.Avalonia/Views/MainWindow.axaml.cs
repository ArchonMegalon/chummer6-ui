using Avalonia.Controls;
using Avalonia.Interactivity;
using System;

namespace AvaloniaProject.Views
{
    public partial class MainWindow : Window
    {
        private readonly EngineClient _engineClient;

        public MainWindow()
        {
            InitializeComponent();
            _engineClient = new EngineClient();
        }

        private async void OnSimulateBuildClicked(object sender, RoutedEventArgs e)
        {
            // Disable the button while processing to prevent multiple clicks
            if (sender is Button triggerButton)
            {
                triggerButton.IsEnabled = false;
            }

            ResultTextBlock.Text = "Executing build simulation... Please wait.";

            try
            {
                string parameters = ParametersTextBox.Text ?? "None";
                
                // Call the mocked asynchronous boundary
                string result = await _engineClient.ExecuteBuildAsync(parameters);
                
                ResultTextBlock.Text = result;
            }
            catch (Exception ex)
            {
                ResultTextBlock.Text = $"Build failed with error: {ex.Message}";
            }
            finally
            {
                // Re-enable the button
                if (sender is Button restoreButton)
                {
                    restoreButton.IsEnabled = true;
                }
            }
        }
    }
}
