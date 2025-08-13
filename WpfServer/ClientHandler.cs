using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Linq;

namespace WPF_Server
{
    public class ClientHandler
    {
        public Socket? ClientSocket { get; private set; }
        private MainWindow serverWindow;
        private AuthenticationManager authenticationManager;


        public string? Username { get; set; }

        private const int BufferSize = 4096;
        private byte[] receiveBuffer = new byte[BufferSize];


        public ClientHandler(Socket socket, MainWindow window, AuthenticationManager authManager)
        {
            ClientSocket = socket;
            serverWindow = window;
            authenticationManager = authManager;

            serverWindow.Log($"Új kliens csatlakozott (kapcsolat): {ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen EndPoint"}");
        }

        public async Task HandleClientAsync()
        {
            try
            {

                if (!await AuthenticateClientAsync())
                {

                    serverWindow.Log($"Kliens ({ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen EndPoint"}) autentikáció sikertelen vagy megszakadt.");

                    return;
                }


                serverWindow.Log($"Kliens ({ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen EndPoint"}) sikeresen bejelentkezett mint: {Username}");


                serverWindow.BroadcastActiveUsers();

                int bytesRead;

                while (ClientSocket != null && ClientSocket.Connected)
                {
                    try
                    {

                        if (ClientSocket == null || !ClientSocket.Connected) break;


                        bytesRead = await ClientSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, 0, BufferSize), SocketFlags.None);

                        if (bytesRead > 0)
                        {

                            string receivedMessage = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);


                            if (receivedMessage.ToUpper() == "DISCONNECT")
                            {

                                serverWindow.Log($"Kliens ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}) küldte a DISCONNECT parancsot.");
                                break;
                            }

                            else if (receivedMessage.StartsWith("@"))
                            {

                                string[] parts = receivedMessage.Split(' ', 2);


                                if (parts.Length == 2)
                                {
                                    string recipientUsername = parts[0].Substring(1);
                                    string privateMessageContent = parts[1];


                                    if (!string.IsNullOrWhiteSpace(recipientUsername))
                                    {

                                        serverWindow.SendPrivateMessage(recipientUsername, $"[{Username}] (privát): {privateMessageContent}", this);
                                    }
                                    else
                                    {

                                        SendMessage("Szerver: Hibás privát üzenet formátum. Nincs megadva címzett felhasználónév az '@' után.");
                                    }
                                }
                                else
                                {

                                    SendMessage("Szerver: Hibás privát üzenet formátum. Használat: @Felhasználónév ÜzenetSzövege");
                                }
                            }
                            else
                            {

                                serverWindow.BroadcastMessage(receivedMessage, this);
                            }

                        }
                        else
                        {

                            serverWindow.Log($"Kliens ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}) lezárta a kapcsolatot (0 byte received).");
                            break;
                        }
                    }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset ||
                                                     ex.SocketErrorCode == SocketError.ConnectionAborted ||
                                                     ex.SocketErrorCode == SocketError.Shutdown)
                    {
                        serverWindow.Log($"Kliens ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}) kapcsolat megszakadt (SocketException): {ex.Message}");
                        break;
                    }
                    catch (ObjectDisposedException)
                    {

                        serverWindow.Log($"Kliens socket ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}) már lezárult (ObjectDisposedException).");
                        break;
                    }
                    catch (Exception ex)
                    {

                        serverWindow.Log($"Hiba a kliens ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}) üzenetkezelése során: {ex.Message}");
                        break;
                    }
                }


            }
            catch (Exception ex)
            {
                serverWindow.Log($"Általános hiba a klienskezelő taskban ({ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen EndPoint"}): {ex.Message}");
            }
            finally
            {

                serverWindow.RemoveClient(this);

                try
                {

                    if (ClientSocket != null && ClientSocket.Connected)
                    {
                        ClientSocket.Shutdown(SocketShutdown.Both);
                    }
                }
                catch { }

                ClientSocket?.Close();
                ClientSocket = null;

                serverWindow.Log($"Kliens kezelő befejeződött ({Username ?? "ismeretlen felhasználó"}).");
            }
        }


        private async Task<bool> AuthenticateClientAsync()
        {

            string clientIdentifier = ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen EndPoint";
            serverWindow.Log($"Autentikáció indítása kliensnek ({clientIdentifier})...");

            try
            {

                int bytesRead = await ClientSocket!.ReceiveAsync(new ArraySegment<byte>(receiveBuffer, 0, BufferSize), SocketFlags.None);

                if (bytesRead == 0)
                {

                    serverWindow.Log($"Autentikáció sikertelen: Kliens ({clientIdentifier}) lezárta a kapcsolatot bejelentkezés előtt (0 byte received).");

                    ClientSocket?.Close();
                    ClientSocket = null;
                    return false;
                }


                string receivedData = Encoding.UTF8.GetString(receiveBuffer, 0, bytesRead);
                serverWindow.Log($"Autentikációs üzenet fogadva ({clientIdentifier}): '{receivedData}'");


                if (receivedData.StartsWith("LOGIN:"))
                {

                    string[] parts = receivedData.Split(':', 3);
                    if (parts.Length == 3)
                    {
                        string usernameAttempt = parts[1];
                        string passwordAttempt = parts[2];

                        serverWindow.Log($"Bejelentkezési kísérlet ({clientIdentifier}): Felhasználónév='{usernameAttempt}', Jelszó='{passwordAttempt}'");


                        if (authenticationManager.AuthenticateUser(usernameAttempt, passwordAttempt))
                        {

                            bool isAlreadyLoggedIn = false;
                            lock (serverWindow.connectedClients)
                            {

                                isAlreadyLoggedIn = serverWindow.connectedClients.Any(c => c != this && c.Username != null && c.Username.Equals(usernameAttempt, StringComparison.OrdinalIgnoreCase));
                            }

                            if (isAlreadyLoggedIn)
                            {

                                await SendMessageAsync("LOGIN_ALREADY_LOGGED_IN");
                                serverWindow.Log($"Bejelentkezési kísérlet elutasítva ({clientIdentifier}): Felhasználó '{usernameAttempt}' már online.");

                                try { ClientSocket?.Shutdown(SocketShutdown.Both); } catch { }
                                ClientSocket?.Close();
                                ClientSocket = null;
                                return false;
                            }
                            else
                            {

                                Username = usernameAttempt;

                                await SendMessageAsync("LOGIN_SUCCESS");
                                serverWindow.Log($"Autentikáció sikeres kliensnek ({clientIdentifier}): {Username}");
                                return true;
                            }


                        }
                        else
                        {

                            await SendMessageAsync("LOGIN_FAILED");
                            serverWindow.Log($"Autentikáció sikertelen kliensnek ({clientIdentifier}): Hibás felhasználónév vagy jelszó.");

                            try { ClientSocket?.Shutdown(SocketShutdown.Both); } catch { }
                            ClientSocket?.Close();
                            ClientSocket = null;
                            return false;
                        }
                    }
                    else
                    {

                        await SendMessageAsync("LOGIN_ERROR: Hibás LOGIN formátum.");
                        serverWindow.Log($"Autentikáció sikertelen kliensnek ({clientIdentifier}): Hibás LOGIN üzenet formátum.");
                        try { ClientSocket?.Shutdown(SocketShutdown.Both); } catch { }
                        ClientSocket?.Close();
                        ClientSocket = null;
                        return false;
                    }
                }
                else
                {

                    await SendMessageAsync("LOGIN_REQUIRED: Kérlek, először jelentkezz be.");
                    serverWindow.Log($"Autentikáció sikertelen kliensnek ({clientIdentifier}): Nem LOGIN üzenettel kezdett.");
                    try { ClientSocket?.Shutdown(SocketShutdown.Both); } catch { }
                    ClientSocket?.Close();
                    ClientSocket = null;
                    return false;
                }
            }

            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset ||
                                            ex.SocketErrorCode == SocketError.ConnectionAborted ||
                                            ex.SocketErrorCode == SocketError.Shutdown)
            {
                serverWindow.Log($"Autentikáció megszakadt SocketException miatt ({clientIdentifier}): {ex.Message}");

                try { ClientSocket?.Shutdown(SocketShutdown.Both); } catch { }
                ClientSocket?.Close();
                ClientSocket = null;
                return false;
            }
            catch (ObjectDisposedException)
            {
                serverWindow.Log($"Autentikáció megszakadt ObjectDisposedException miatt ({clientIdentifier}). Socket lezárult.");
                ClientSocket = null;
                return false;
            }
            catch (Exception ex)
            {
                serverWindow.Log($"Autentikáció során váratlan hiba ({clientIdentifier}): {ex.Message}");

                if (ClientSocket != null && ClientSocket.Connected)
                {
                    try
                    {
                        byte[] errorBytes = Encoding.UTF8.GetBytes("LOGIN_ERROR: Váratlan hiba a szerveren.");
                        await ClientSocket.SendAsync(new ArraySegment<byte>(errorBytes), SocketFlags.None);
                    }
                    catch { }
                }

                try { ClientSocket?.Shutdown(SocketShutdown.Both); } catch { }
                ClientSocket?.Close();
                ClientSocket = null;
                return false;
            }
        }

      
        // Ezt csak az AuthenticateClientAsync hívja
        private async Task SendMessageAsync(string message)
        {

            if (ClientSocket == null || !ClientSocket.Connected)
            {
            }

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);

                await ClientSocket.SendAsync(new ArraySegment<byte>(data), SocketFlags.None);
            }
            catch (SocketException ex)
            {
                serverWindow.Log($"Socket hiba ASYNC üzenetküldés közben kliensnek ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}): {ex.Message}");
            }
            catch (ObjectDisposedException)
            {
                serverWindow.Log($"ObjectDisposedException ASYNC üzenetküldés közben kliensnek ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}). Socket lezárult.");
            }
            catch (Exception ex)
            {
                serverWindow.Log($"Általános hiba ASYNC üzenetküldés közben kliensnek ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}): {ex.Message}");
            }
        }

        public void SendMessage(string message)
        {

            if (ClientSocket == null || !ClientSocket.Connected)
            {
                return;
            }

            try
            {
                byte[] data = Encoding.UTF8.GetBytes(message);

                ClientSocket.Send(data);
            }
            catch (SocketException ex)
            {

                serverWindow.Log($"Socket hiba SYNC üzenetküldés közben kliensnek ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}): {ex.Message}");
            }
            catch (ObjectDisposedException)
            {

                serverWindow.Log($"ObjectDisposedException SYNC üzenetküldés közben kliensnek ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}). Socket lezárult.");
            }
            catch (Exception ex)
            {
                serverWindow.Log($"Általános hiba SYNC üzenetküldés közben kliensnek ({Username ?? ClientSocket?.RemoteEndPoint?.ToString() ?? "ismeretlen"}): {ex.Message}");
            }
        }


        public void SendActiveUsersList(string activeUsersListString)
        {
            SendMessage(activeUsersListString);
        }


    }
}