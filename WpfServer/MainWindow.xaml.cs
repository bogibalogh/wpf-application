using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading; 
using System.Diagnostics;

namespace WPF_Server 
{
    public partial class MainWindow : Window
    {
        private Socket? serverSocket;
        public readonly List<ClientHandler> connectedClients = new List<ClientHandler>(); //  az összes jelenleg csatlakozott kliens kezelőjét tárolja
        private readonly object clientsLock = new object();
        private AuthenticationManager authenticationManager;

        public MainWindow()
        {
            InitializeComponent();
            authenticationManager = new AuthenticationManager();
            this.Closing += OnWindowClosing; 
        }

     
        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            try
            {
                if (serverSocket != null)
                {   
                    serverSocket.Close();
                    serverSocket = null;  
                }

            }
            catch (Exception ex)
            {
                // Debug output a kivételről, ha a bezárás közben hiba történik (pl. már bezárták)
                Debug.WriteLine($"Server MainWindow: Error closing server socket in OnWindowClosing: {ex.Message}");
                Debug.WriteLine($"Server MainWindow: Exception Type: {ex.GetType().Name}");
                Debug.WriteLine($"Server MainWindow: Stack Trace: {ex.StackTrace}");
            }
            Debug.WriteLine("Server MainWindow: OnWindowClosing END.");
        }


        private async void StartServerButton_Click(object sender, RoutedEventArgs e)
        {
            //Megakadályozza, hogy a szerver többször is elinduljon
            if (serverSocket != null)
            {
                Log("A szerver már fut.");
                Debug.WriteLine("Server MainWindow: Server already running.");
                return;
            }

            StartServerButton.IsEnabled = false; //Gomb kikapcsolás miközben fut a szerver
            LogTextBox.Clear();
            Log("Szerver indítása...");

            try
            {   //Socket inicializálás
                serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPAddress iPAddress = IPAddress.Parse("127.0.0.1");
                IPEndPoint iPEndPoint = new IPEndPoint(iPAddress, 8888);

                serverSocket.Bind(iPEndPoint);
                serverSocket.Listen(10);
                Log("Szerver elindult. Várakozás kliensekre...");


                //Fogadási ciklus (aszinkron) 
                while (serverSocket != null) 
                {
                    Socket clientSocket = await serverSocket.AcceptAsync();
                    var clientHandler = new ClientHandler(clientSocket, this, authenticationManager);
                   

                    lock (clientsLock)
                    {
                        connectedClients.Add(clientHandler);
                      
                    }
                    _ = Task.Run(() => clientHandler.HandleClientAsync());  //  clientHandler.HandleClientAsync() metódust futtatja   A _ = azt jelenti, hogy nem várunk a task befejezésére, hanem azonnal visszatérünk.

                }
               
            }
            catch (SocketException ex)
            {

                bool isExpectedAbort = ex.SocketErrorCode == SocketError.OperationAborted ||
                                       ex.SocketErrorCode == SocketError.Interrupted ||
                                       ex.SocketErrorCode == SocketError.Shutdown; 


                Log($"Socket hiba a szerver futása során: {ex.Message} (ErrorCode: {ex.SocketErrorCode})");

                if (!isExpectedAbort)
                {
                    MessageBox.Show($"Váratlan szerver futási hiba: {ex.Message} (ErrorCode: {ex.SocketErrorCode})", "Szerver hiba");
                    Debug.WriteLine("Server MainWindow: Showing MessageBox for unexpected SocketException.");
                }
                else
                {
                    Debug.WriteLine("Server MainWindow: Caught expected SocketException (Abort/Interrupted/Shutdown) during shutdown."); 
                }

             
                Dispatcher.Invoke(() => { 
                                        
                    if (serverSocket == null)
                    {
                        Log("Szerver leállt.");
                    }
                    else 
                    {
                        StartServerButton.IsEnabled = true;
                        Log("Szerver leállt hiba miatt.");
                    }
                });
                serverSocket?.Close();
                serverSocket = null;
            }
            catch (ObjectDisposedException)
            {
                Log("Szerver socket lezárva, a szerver leállt.");
                Dispatcher.Invoke(() => StartServerButton.IsEnabled = true);
                serverSocket = null;
            }
            catch (Exception ex)
            {         
                Log($"Váratlan hiba a szerver futása közben: {ex.Message}");
                MessageBox.Show($"Váratlan szerver hiba: {ex.Message}", "Szerver hiba");
                Dispatcher.Invoke(() => StartServerButton.IsEnabled = true);
                serverSocket?.Close();
                serverSocket = null;
            }
            Debug.WriteLine("Server MainWindow: StartServerButton_Click END");
        }



        // minden csatlakozott kliensnek elküld egy üzenetet.
        public void BroadcastMessage(string message, ClientHandler? sender) {

            string senderIdentifier = sender?.Username ?? sender?.ClientSocket?.RemoteEndPoint?.ToString() ?? "Szerver";
            Dispatcher.Invoke(() => Log($"[BROADCAST ({senderIdentifier})] {message}"));

            List<ClientHandler> clientsToSendTo;

            lock (clientsLock) //szálbiztosan hozzáférünk a connectedClients listához.
            {
                clientsToSendTo = connectedClients.Where(c => c.Username != null).ToList();
                
            }

            foreach (var client in clientsToSendTo)
            {
                client.SendMessage(message); 
            }
        }


        // Privát üzenet
        public void SendPrivateMessage(string recipientUsername, string message, ClientHandler sender) {
            ClientHandler? recipient = null;

            lock (clientsLock) //szálbiztosan hozzáférünk a connectedClients listához.
            {
                recipient = connectedClients.FirstOrDefault(c => c.Username != null && c.Username.Equals(recipientUsername, StringComparison.OrdinalIgnoreCase));
            }

            string senderIdentifier = sender?.Username ?? sender?.ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen";

            if (recipient != null)
            {
                
                string formattedMessage = $"[{sender.Username}] (privát): {message}";
                recipient.SendMessage(formattedMessage); 
               
                Dispatcher.Invoke(() => Log($"[PRIVÁT {senderIdentifier} -> {recipientUsername}]: {message}")); 
                
            }
            else
            {
                sender.SendMessage($"Szerver: A felhasználó '{recipientUsername}' nem található vagy nincs online.");
                 Dispatcher.Invoke(() => Log($"[PRIVÁT HIBA {senderIdentifier}]: Címzett '{recipientUsername}' nem található."));
            }
        }

        //Aktív Felhasználók Listája
        public void BroadcastActiveUsers() { 
            List<ClientHandler> activeClients;

            lock (clientsLock)
            { 
                activeClients = connectedClients.Where(c => c.Username != null).ToList();
            }

            
            string activeUsersListString = "ACTIVE_USERS: " + string.Join(",", activeClients.Select(c => c.Username));
          
            Dispatcher.Invoke(() => Log($"Aktív felhasználók listája összeállítva: {activeUsersListString}"));

            foreach (var client in activeClients)
            {
                client.SendActiveUsersList(activeUsersListString);
            }
            
        }
       
        //KLiens lecsatlakozása
        public void RemoveClient(ClientHandler client) {
      
            string clientIdentifier = client?.Username ?? client?.ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"; 
            bool removed = false;


            lock (clientsLock) 
            {
                removed = connectedClients.Remove(client);           
            }

            if (removed)
            {
                Log($"Kliens ({clientIdentifier}) eltávolítva. Aktív kapcsolatok száma: {connectedClients.Count}");
                 
                if (client?.Username != null)
                {
                    BroadcastActiveUsers(); 
          
                    BroadcastMessage($"[{client.Username}] lecsatlakozott a chatről.", null); 
                }
                else
                {
                    Log($"Nem bejelentkezett kliens ({clientIdentifier}) lecsatlakozott."); 
                }
            }

        }
    
        public void Log(string message) {

            if (!Dispatcher.CheckAccess())
            {
         
                Dispatcher.Invoke(() =>
                {
                    LogTextBox.AppendText(message + Environment.NewLine);
                    LogTextBox.ScrollToEnd(); 
                });
            }
            else
            {
                 LogTextBox.AppendText(message + Environment.NewLine);
                LogTextBox.ScrollToEnd(); 
            }
        }
    }
 }
