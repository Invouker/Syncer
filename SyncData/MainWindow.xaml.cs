using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Win32;
using MessageBox = System.Windows.MessageBox;

namespace SyncData
{
    public partial class MainWindow : Window
    {
        private string host;
        private string login;
        private string password;
        private string remotePath;
        private string localPath;

        public MainWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private readonly string configFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SyncData/SyncAppSettings.data");
        
        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            host = HostTextBox.Text;
            login = LoginTextBox.Text;
            password = PasswordBox.Password;
            remotePath = RemotePathTextBox.Text;
            localPath = LocalPathTextBox.Text;

            SaveSettings();
            StatusText.Text = "Settings has been saved.";
            //MessageBox.Show("Settings saved successfully.");
        }

        private void StartSync_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password)
                || string.IsNullOrEmpty(remotePath) || string.IsNullOrEmpty(localPath))
            {
                MessageBox.Show("Please enter all required information.");
                return;
            }

            StatusText.Text = "Sync in progress...";

            // Spustiť synchronizáciu na pozadí
            Task.Run(() => PerformSync());
        }

        private void PerformSync()
        {
            // Implementujte synchronizáciu súborov zo zadaných priečinkov
            // Príklad: Kopírovanie všetkých súborov z lokalného priečinka do vzdialeného
            try
            {
                string[] files = Directory.GetFiles(localPath);
                foreach (string file in files)
                {
                    string fileName = Path.GetFileName(file);
                    string destinationPath = Path.Combine(remotePath, fileName);
                    File.Copy(file, destinationPath, true);
                }

                // Zobrazte správu o úspechu
                Dispatcher.Invoke(() => StatusText.Text = "Sync completed successfully.");
            }
            catch (Exception ex)
            {
                // Zobrazte chybovú správu
                Dispatcher.Invoke(() => StatusText.Text = $"Error during sync: {ex.Message}");
            }
        }
        
        private void SaveSettings()
        {
            string dataToSave = $"{host};{login};{password};{remotePath};{localPath}";
            // Convert the string to bytes
            Debug.WriteLine(dataToSave);
            byte[] buffer = System.Text.Encoding.UTF8.GetBytes(dataToSave);

            // Use DPAPI to protect the data
            byte[] encryptedData = ProtectedData.Protect(buffer, null, DataProtectionScope.CurrentUser);
            Debug.WriteLine(encryptedData);

            // Save settings to the configuration file
            File.WriteAllBytes(configFilePath, encryptedData);
        }

        private void LoadSettings()
        {
            // Load settings from the configuration file
            if (File.Exists(configFilePath))
            {
                
                // Use DPAPI to unprotect the data
                byte[] decryptedData = ProtectedData.Unprotect(File.ReadAllBytes(configFilePath), null, DataProtectionScope.CurrentUser);

                // Convert the decrypted bytes back to a string
                string result = System.Text.Encoding.UTF8.GetString(decryptedData);
                
                string[] settings = result.Split(';');

                if (settings.Length == 5)
                {
                    host = settings[0];
                    login = settings[1];
                    password = settings[2];
                    remotePath = settings[3];
                    localPath = settings[4];

                    // Update UI with loaded settings
                    HostTextBox.Text = host;
                    LoginTextBox.Text = login;
                    PasswordBox.Password = password;
                    RemotePathTextBox.Text = remotePath;
                    LocalPathTextBox.Text = localPath;
                }
            }
        }
        
        private void SetAutoStart()
        {
            
            string appName = "Syncer";

            // Specify your application's executable path
            //string appPath = @"C:\Path\To\Your\Application.exe";
            string appPath = @"E:\Github\Sync\SyncData\bin\Debug2\SyncData.exe";

            RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            // Check if the auto-start is currently enabled
            bool isAutoStartEnabled = registryKey.GetValue(appName) != null;
            
            if (!isAutoStartEnabled)
            {
                // Enable auto-start
                registryKey.SetValue(appName, appPath);
                StatusText.Text = "Auto app start enabled.";
            }else {
                // Disable auto-start
                registryKey.DeleteValue(appName, false);
                StatusText.Text = "Auto app start disabled.";
            }
        }

        private void BrowseLocalPath_Click(object sender, RoutedEventArgs e)
        {
            using (var folderBrowserDialog = new FolderBrowserDialog())
            {
                DialogResult result = folderBrowserDialog.ShowDialog();

                if (result == System.Windows.Forms.DialogResult.OK && !string.IsNullOrWhiteSpace(folderBrowserDialog.SelectedPath))
                {
                    LocalPathTextBox.Text = folderBrowserDialog.SelectedPath;
                }
            }
        }

        private void ToggleAutoStart_Click(object sender, RoutedEventArgs e)
        {
            // Toggle the auto-start feature
            SetAutoStart();
        }

    }
}