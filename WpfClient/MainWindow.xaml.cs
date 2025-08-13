using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Diagnostics;

namespace WPF_Client
{
    public partial class MainWindow : Window
    {
        private Socket? clientSocket; // A szervertől kapott socket
        public string username { get; set; } // A bejelentkezett felhasználónév

        // Aktív felhasználók listája
        private ObservableCollection<string> activeUsers = new ObservableCollection<string>();

        // Konstruktor: Inicializálja az ablakot a bejelentkezett felhasználó adataival és a socket kapcsolattal
        public MainWindow(Socket socket, string loggedInUsername)
        {
            Debug.WriteLine("MainWindow: Constructor START."); // Hibakeresés: Konstruktor eleje
            InitializeComponent();
            Debug.WriteLine("MainWindow: InitializeComponent() END."); // Hibakeresés: InitializeComponent() vége

            clientSocket = socket;
            username = loggedInUsername; // Beállítjuk a felhasználónevet
            this.Title = $"Chat Kliens - {username}"; // Ablak címének beállítása

            // Adatkötés az aktív felhasználók listájához (XAML-ben activeUsersItemsControl)
             activeUsersItemsControl.ItemsSource = activeUsers;
            Debug.WriteLine("MainWindow: activeUsersItemsControl.ItemsSource set."); // Hibakeresés: ItemsSource beállítva

            // Indítjuk az üzenet fogadó Task-ot egy külön szálon
            _ = Task.Run(() => ReceiveMessagesAsync());

            // Kezeljük az ablak bezárási eseményét, hogy tisztességesen le tudjunk csatlakozni
            this.Closing += MainWindow_Closing;

            // A Send gomb legyen aktív a kezdetektől
            SendMessageButton.IsEnabled = true;

            // Naplózzuk a bejelentkezést a chat ablakban
            Log($"Sikeresen bejelentkezve mint: {username}");
            Log("Üdvözöllek a chat szobában!");
            Debug.WriteLine("MainWindow: Constructor END."); // Hibakeresés: Konstruktor vége
        }

        // Ablak bezárási esemény kezelője
        private async void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            // Ha van aktív kapcsolat, próbáljunk meg lecsatlakozni a szerverről
            if (clientSocket != null && clientSocket.Connected)
            {
                Debug.WriteLine("MainWindow_Closing: Socket is connected, attempting graceful shutdown.");
                try
                {
                    // Elküldjük a DISCONNECT üzenetet a szervernek
                    string disconnectMessage = "DISCONNECT";
                    byte[] disconnectBytes = Encoding.UTF8.GetBytes(disconnectMessage);
                    await clientSocket.SendAsync(new ArraySegment<byte>(disconnectBytes), SocketFlags.None);

                    // Leállítjuk a socketet mindkét irányban
                    clientSocket.Shutdown(SocketShutdown.Both);
                }
                catch (SocketException ex) { Debug.WriteLine($"MainWindow_Closing Socket error during shutdown: {ex.Message}"); }
                catch (ObjectDisposedException) { Debug.WriteLine("MainWindow_Closing ObjectDisposedException during shutdown: Socket already disposed."); }
                catch (Exception ex) { Debug.WriteLine($"MainWindow_Closing Unexpected error during shutdown: {ex.Message}"); }
                finally
                {
                    // Végül bezárjuk a socketet és nullázzuk a referenciát
                    clientSocket?.Close();
                    clientSocket = null;
                }
            }
        }

        // Üzenet fogadó Task - aszinkron fut a háttérben
        private async Task ReceiveMessagesAsync()
        {
            int bytesRead;
            byte[] receiveBuffer = new byte[4096];

            // Ciklus addig fut, amíg a socket létezik és csatlakoztatva van
            while (clientSocket != null && clientSocket.Connected)
            {
                try
                {
                    // Aszinkron módon fogadunk adatokat a szervertől
                    bytesRead = await clientSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), SocketFlags.None);

                    if (bytesRead > 0)
                    {
                        // Dekódoljuk a fogadott üzenetet
                        string receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);

                        // Ellenőrizzük, hogy aktív felhasználók listája érkezett-e
                        if (receivedMessage.StartsWith("ACTIVE_USERS:"))
                        {
                            string usersString = receivedMessage.Substring("ACTIVE_USERS:".Length);
                            // Felosztjuk a felhasználóneveket vessző mentén
                            var users = string.IsNullOrEmpty(usersString) ? new string[0] : usersString.Split(',');

                            // Frissítjük az aktív felhasználók listáját a UI szálon
                            await Dispatcher.InvokeAsync(() =>
                            {
                                try
                                {
                                    activeUsers.Clear(); // Töröljük a régi listát
                                    foreach (var user in users)
                                    {
                                        string trimmedUser = user.Trim();
                                        // Hozzáadjuk a felhasználókat, kivéve a saját felhasználónevet
                                        if (!string.IsNullOrWhiteSpace(user) && trimmedUser != username)
                                        {
                                            activeUsers.Add(trimmedUser);

                                             var container = activeUsersItemsControl.ItemContainerGenerator.ContainerFromItem(trimmedUser) as FrameworkElement;
                                            if (container != null)
                                            {
                                                RadioButton? radioButton = FindVisualChild<RadioButton>(container);
                                             }
                                        }
                                    }
                                }
                                catch (Exception uiEx)
                                {
                                    // Hibakeresés a UI frissítés során fellépő kivételekre
                                    Debug.WriteLine($"!!! ERROR caught in Dispatcher.InvokeAsync UI update:");
                                    Debug.WriteLine($"!!! Message: {uiEx.Message}");
                                    Debug.WriteLine($"!!! Type: {uiEx.GetType().Name}");
                                    Debug.WriteLine($"!!! Stack Trace: {uiEx.StackTrace}");
                                }
                            });
                        }
                        else
                        {
                            // Ha nem aktív felhasználók listája, akkor normál chat üzenet
                            await Dispatcher.InvokeAsync(() => Log(receivedMessage));
                        }
                    }
                    else 
                    {
                        await Dispatcher.InvokeAsync(() => Log("A szerver lezárta a kapcsolatot."));
                        break; // Kilépés a fogadó ciklusból
                    }
                    Debug.WriteLine("ReceiveMessagesAsync: End of try block in while loop iteration.");
                }
                // Hibakezelés a socket kommunikáció során
                catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset ||
                                                 ex.SocketErrorCode == SocketError.ConnectionAborted ||
                                                 ex.SocketErrorCode == SocketError.Shutdown)
                {
                    await Dispatcher.InvokeAsync(() => Log($"Szerverkapcsolat megszakadt: {ex.Message}"));
                    break; // Kilépés a fogadó ciklusból
                }
                catch (ObjectDisposedException)
                {
                    await Dispatcher.InvokeAsync(() => Log("A kapcsolat már bezárult."));
                    break; // Kilépés a fogadó ciklusból
                }
                catch (Exception ex)
                {
                    // Általános hiba a receive loopban (nem a UI frissítésben)
                    Debug.WriteLine($"ReceiveMessagesAsync Generic Exception caught: {ex.Message}");
                    Debug.WriteLine($"ReceiveMessagesAsync Exception Type: {ex.GetType().Name}");
                    Debug.WriteLine($"ReceiveMessagesAsync Stack Trace: {ex.StackTrace}");
                    await Dispatcher.InvokeAsync(() => Log($"Hiba történt az üzenet fogadása közben: {ex.Message}"));
                    break; // Kilépés a fogadó ciklusból
                }
            }

            // A fogadó ciklus befejezése után zárjuk be a socketet, ha még nyitva van
            if (clientSocket != null)
            {
                clientSocket.Close();
                clientSocket = null;
            }

            // Letiltjuk az üzenetküldő gombot a UI szálon
            await Dispatcher.InvokeAsync(() => {
                SendMessageButton.IsEnabled = false;
            });
        }

        // Naplózó metódus, amely üzeneteket ír a LogTextBox-ba (UI szálon)
        private void Log(string message)
        {
            // Ellenőrizzük, hogy a hívás a UI szálról érkezett-e
            if (!Dispatcher.CheckAccess())
            {
                // Ha nem, delegáljuk a UI szálra
                Dispatcher.Invoke(() =>
                {
                    LogTextBox.AppendText(message + Environment.NewLine);
                    LogTextBox.ScrollToEnd(); // Görgessünk a legújabb üzenetre
                });
            }
            else
            {
                // Ha a UI szálról érkezett, közvetlenül frissítjük
                LogTextBox.AppendText(message + Environment.NewLine);
                LogTextBox.ScrollToEnd(); // Görgessünk a legújabb üzenetre
            }
        }

        // Üzenetküldő gomb kattintás eseménye
        private async void SendMessageButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageTextBox == null)
            {
                return;
            }

            string messageToSend = MessageTextBox.Text.Trim();
            if (string.IsNullOrEmpty(messageToSend))
            {
                return;
            }

            // Csak akkor küldünk üzenetet, ha van aktív kapcsolat
            if (clientSocket != null && clientSocket.Connected)
            {
                string? recipient = null;

                // Megkeressük, hogy van-e kiválasztott privát címzett a RadioButton-ok között
                foreach (string userNameInList in activeUsers)
                {
                    var container = activeUsersItemsControl.ItemContainerGenerator.ContainerFromItem(userNameInList) as FrameworkElement;

                    if (container != null)
                    {
                        RadioButton? radioButton = FindVisualChild<RadioButton>(container);

                        if (radioButton != null && radioButton.IsChecked == true)
                        {
                            recipient = userNameInList; // Megtaláltuk a kiválasztott címzettet
                            break; 
                        }
                    }
                }

                try
                {
                    string finalMessageToSend;

                    if (!string.IsNullOrEmpty(recipient))
                    {
                        // Privát üzenet formázása
                        finalMessageToSend = $"@{recipient} {messageToSend}";
                        Log($"-> Privát [{recipient}]: {messageToSend}");
                    }
                    else
                    {
                        // Nyilvános üzenet
                        finalMessageToSend = messageToSend;
                        Log($"-> Mindenki: {messageToSend}");
                    }

                    // Elküldjük az üzenetet a szervernek
                    byte[] buffer = Encoding.UTF8.GetBytes(finalMessageToSend);
                    await clientSocket.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

                    // Töröljük az üzenet beviteli mező tartalmát a UI szálon
                    await Dispatcher.InvokeAsync(() => MessageTextBox.Clear());

                }
                // Hibakezelés az üzenetküldés során
                catch (SocketException ex) { Debug.WriteLine($"SendMessageButton_Click Socket error: {ex.Message}"); await Dispatcher.InvokeAsync(() => Log($"Hiba történt az üzenet küldése közben: {ex.Message}")); }
                catch (ObjectDisposedException) { Debug.WriteLine("SendMessageButton_Click ObjectDisposedException: Socket disposed."); await Dispatcher.InvokeAsync(() => Log("Nem lehet üzenetet küldeni, a kapcsolat már bezárult.")); await Dispatcher.InvokeAsync(() => SendMessageButton.IsEnabled = false); }
                catch (Exception ex) { Debug.WriteLine($"SendMessageButton_Click Unexpected error: {ex.Message}"); await Dispatcher.InvokeAsync(() => Log($"Váratlan hiba történt az üzenet küldése közben: {ex.Message}")); }
            }
            else
            {
                // Ha nincs kapcsolat, figyelmeztetést írunk ki és letiltjuk a gombot
                await Dispatcher.InvokeAsync(() => Log("Nincs kapcsolat a szerverrel. Kérlek, csatlakozz újra a Login ablakon keresztül."));
                SendMessageButton.IsEnabled = false;
            }
        }

        // Segédmetódus a vizuális fa bejárásához és egy adott típusú gyermekelem megkereséséhez
        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                if (child is T visualChild)
                {
                    // Ha megtaláltuk a keresett típusú elemet, visszaadjuk
                    return visualChild;
                }
                // Rekurzívan keresünk a gyermekelemekben
                T? deeperLevel = FindVisualChild<T>(child);
                if (deeperLevel != null)
                {
                    return deeperLevel;
                }
            }
            return null; // Nem találtuk meg
        }

        // Üzenetküldés Enter billentyű lenyomására
        private void MessageTextBox_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (SendMessageButton.IsEnabled)
                {
                    SendMessageButton_Click(sender, e);
                }
            }
        }
    }
}
