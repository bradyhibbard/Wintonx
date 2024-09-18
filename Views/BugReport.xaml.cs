using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Winton.Views
{
    public partial class BugReport : UserControl
    {
        private const string GitHubOwner = "bradyhibbard";  // Replace with your GitHub username or organization name
        private const string GitHubRepo = "Winton";  // Replace with the name of your GitHub repository
        private const string GitHubToken = "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";  // Replace with your GitHub Personal Access Token (keep it secure)

        public BugReport()
        {
            InitializeComponent();
        }

        private async void SendBugReport_Click(object sender, RoutedEventArgs e)
        {
            string title = TitleTextBox.Text;
            string description = DescriptionTextBox.Text;
            string stepsToReproduce = StepsTextBox.Text;
            string expectedBehavior = ExpectedTextBox.Text;
            string actualBehavior = ActualTextBox.Text;
            string environmentInfo = EnvironmentTextBox.Text;

            // Check if required fields are filled
            if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            {
                MessageBox.Show("Please fill in all fields before submitting.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                string responseMessage = await CreateGitHubIssue(title, description, stepsToReproduce, expectedBehavior, actualBehavior, environmentInfo);
                MessageBox.Show(responseMessage, "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending bug report: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task<string> CreateGitHubIssue(string title, string description, string steps, string expected, string actual, string environment)
        {
            using (HttpClient client = new HttpClient())
            {
                // Set up the request headers
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", GitHubToken);
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("BugReportApp", "1.0"));

                // Format the issue body with markdown for better readability
                string issueBody = $"### Description\n{description}\n\n" +
                                   $"### Steps to Reproduce\n{steps}\n\n" +
                                   $"### Expected Behavior\n{expected}\n\n" +
                                   $"### Actual Behavior\n{actual}\n\n" +
                                   $"### Environment Info\n{environment}";

                // Set up the issue data
                var issueData = new
                {
                    title = title,
                    body = issueBody
                };

                // Serialize the data into JSON
                string jsonData = Newtonsoft.Json.JsonConvert.SerializeObject(issueData);
                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");

                // Make the POST request to create an issue
                string url = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/issues";
                HttpResponseMessage response = await client.PostAsync(url, content);

                if (response.IsSuccessStatusCode)
                {
                    return "Bug report submitted successfully!";
                }
                else
                {
                    string errorMessage = await response.Content.ReadAsStringAsync();
                    return $"Failed to submit bug report. GitHub API response: {errorMessage}";
                }
            }
        }
    }
}
