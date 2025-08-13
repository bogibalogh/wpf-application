using System;
using System.ComponentModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Diagnostics;

namespace WPF_Client
{
    public partial class LoginWindow : Window
    {
        private Socket? clientSocket;
        private const string serverIP = "127.0.0.1";
        private const int serverPort = 8888;

        public LoginWindow()
        {
            InitializeComponent();
            this.Closing += LoginWindow_Closing;
        }


        private void LoginWindow_Closing(object? sender, CancelEventArgs e)
        {
          
        }

        private async void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            // Ellenőrizzük, hogy a UI elemek inicializálva vannak-e.
            if (usernameTextBox == null || passwordBox == null || LoginButton == null)
            {
                MessageBox.Show("A felhasználói felület elemei nincsenek inicializálva.", "Hiba");
                return;
            }

            string username = usernameTextBox.Text.Trim();
            string password = passwordBox.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Kérlek, add meg a felhasználónevet és a jelszót!", "Bejelentkezési hiba");
                return;
            }

 
            LoginButton.IsEnabled = false;

            try
            {
                // Ha nincs aktív kapcsolat, hozzunk létre újat és csatlakozzunk
                if (clientSocket == null || !clientSocket.Connected)
                {
                    clientSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        await clientSocket.ConnectAsync(serverIP, serverPort);
                    }
                    catch (SocketException ex)
                    {
                        MessageBox.Show($"Nem sikerült csatlakozni a szerverhez: {ex.Message}", "Kapcsolódási hiba");
                        clientSocket?.Close(); // Hiba esetén zárjuk be a socket-et
                        clientSocket = null;
                        LoginButton.IsEnabled = true; // Aktiváljuk újra a gombot
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Váratlan hiba a kapcsolódás során: {ex.Message}", "Kapcsolódási hiba");
                        clientSocket?.Close(); // Hiba esetén zárjuk be a socket-et
                        clientSocket = null;
                        LoginButton.IsEnabled = true; // Aktiváljuk újra a gombot
                        return;
                    }
                }

                // Elküldjük a bejelentkezési adatokat a szervernek
                string loginData = $"LOGIN:{username}:{password}";
                byte[] dataToSend = Encoding.UTF8.GetBytes(loginData);
                await clientSocket.SendAsync(new System.ArraySegment<byte>(dataToSend), SocketFlags.None);

                // Várunk a szerver válaszára
                byte[] buffer = new byte[1024];
                int bytesReceived = await clientSocket.ReceiveAsync(new System.ArraySegment<byte>(buffer), SocketFlags.None);

                if (bytesReceived == 0)
                {
                    MessageBox.Show("A szerver lezárta a kapcsolatot a bejelentkezés előtt.", "Kapcsolati hiba");
                    clientSocket?.Close();
                    clientSocket = null;
                    LoginButton.IsEnabled = true;
                    return;
                }

                string response = Encoding.UTF8.GetString(buffer, 0, bytesReceived);

                if (response.StartsWith("LOGIN_SUCCESS"))
                {
                    Debug.WriteLine("LoginWindow: LOGIN_SUCCESS received. Creating MainWindow.");
                    // Sikeres bejelentkezés esetén megnyitjuk a chat ablakot
                    MainWindow chatWindow = new MainWindow(clientSocket, username);
                    Debug.WriteLine("LoginWindow: MainWindow created. Calling Show().");
                    chatWindow.Show();

                    // Most már bezárhatjuk a login ablakot
                    // Ez a hívás kiváltja a LoginWindow_Closing eseményt, de az már nem zárja a socketet.
                    Debug.WriteLine("LoginWindow: MainWindow.Show() called. Closing LoginWindow.");
                    this.Close();
                    Debug.WriteLine("LoginWindow: LoginWindow.Close() called.");
                }
                else if (response.StartsWith("LOGIN_FAILED"))
                {
                    MessageBox.Show("Sikertelen bejelentkezés. Kérlek, ellenőrizd a felhasználónevet és a jelszót!", "Bejelentkezési hiba");
                    clientSocket?.Close(); // Sikertelen login után bezárjuk a socketet
                    clientSocket = null;
                    LoginButton.IsEnabled = true;
                }
                else if (response.StartsWith("LOGIN_ALREADY_LOGGED_IN"))
                {
                    MessageBox.Show($"A felhasználó '{username}' már be van jelentkezve.", "Bejelentkezési hiba");
                    clientSocket?.Close(); // A szerver már lezárta, mi is bezárjuk a kliens oldalon
                    clientSocket = null;
                    LoginButton.IsEnabled = true;
                }
                else if (response.StartsWith("LOGIN_REQUIRED"))
                {
                    MessageBox.Show("A szerver bejelentkezést igényel. Kérlek, add meg az adatokat.", "Információ");
                    LoginButton.IsEnabled = true;
                }
                else if (response.StartsWith("LOGIN_ERROR:"))
                {
                    string errorMessage = response.Substring("LOGIN_ERROR:".Length).Trim();
                    MessageBox.Show($"Hiba történt a bejelentkezés során a szerveren: {errorMessage}", "Szerver hiba");
                    clientSocket?.Close(); // Hiba esetén bezárjuk a socketet
                    clientSocket = null;
                    LoginButton.IsEnabled = true;
                }
                else
                {
                    MessageBox.Show($"Váratlan válasz a szervertől a bejelentkezés során: {response}", "Váratlan válasz");
                    clientSocket?.Close(); // Váratlan válasz esetén bezárjuk a socketet
                    clientSocket = null;
                    LoginButton.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LoginWindow: General Exception in LoginButton_Click: {ex.Message}");
                Debug.WriteLine($"LoginWindow: Exception Type: {ex.GetType().Name}");
                Debug.WriteLine($"LoginWindow: Stack Trace: {ex.StackTrace}");
                MessageBox.Show($"Általános hiba történt a bejelentkezés során: {ex.Message}", "Bejelentkezési hiba");
                clientSocket?.Close(); // Hiba esetén bezárjuk a socketet
                clientSocket = null;
                LoginButton.IsEnabled = true;
                Debug.WriteLine("LoginWindow: General Exception. Socket closed, button enabled. Returning.");
            }
            finally
            {
                Debug.WriteLine("LoginWindow: LoginButton_Click FINALLY block.");
                Debug.WriteLine("LoginWindow: LoginButton_Click FINALLY block END.");
            }
            Debug.WriteLine("LoginWindow: LoginButton_Click END.");
        }

        // Ablak mozgatása egérrel
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }
    }
}
