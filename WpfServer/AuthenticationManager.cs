using System;
using System.Collections.Generic; 
using System.IO; 
using System.Linq; 
using System.Text; 
using System.Threading.Tasks;


namespace WPF_Server 
{
    public class AuthenticationManager
    {
      
        private Dictionary<string, string> users = new Dictionary<string, string>();
     
        private readonly string usersFilePath = "users.txt";

       
        public AuthenticationManager()
        {
            Console.WriteLine("AuthenticationManager példányosítva."); 
            LoadUsersFromFile(); 
        }

       
        private void LoadUsersFromFile()
        {
            // Meghatározzuk a users.txt fájl teljes elérési útját (az alkalmazás futtatható fájljának mappájában)
            string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, usersFilePath);
            Console.WriteLine($"Felhasználók betöltése innen: {fullPath}"); 

            try
            {
                // Ellenőrizzük, hogy a fájl létezik-e
                if (File.Exists(fullPath))
                {
                    Console.WriteLine("users.txt fájl megtalálva."); 
                    // Beolvassuk a fájl összes sorát
                    string[] lines = File.ReadAllLines(fullPath);
                    Console.WriteLine($"Beolvasott sorok száma: {lines.Length}"); 

                    int usersLoaded = 0; // Számláló a sikeresen betöltött felhasználóknak

                    // Végigmegyünk minden soron a fájlban
                    foreach (string line in lines)
                    {
                        Console.WriteLine($"Feldolgozandó sor: '{line}'"); 

                        // Minden sort kettéosztunk a ':' karakter mentén
                        string[] parts = line.Split(':');
                        // Elvárjuk, hogy pontosan két rész legyen (felhasználónév és jelszó)
                        if (parts.Length == 2)
                        {
                            string username = parts[0].Trim();
                            string password = parts[1].Trim();

                            Console.WriteLine($" - Kiszedett adatok: Felhasználónév='{username}', Jelszó='{password}'"); 

                            // Ellenőrizzük, hogy a kiszedett felhasználónév és jelszó nem üres
                            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
                            {
                                // Ellenőrizzük, hogy a felhasználónév még nincs a Dictionary-ban
                                if (!users.ContainsKey(username))
                                {
                                    // Hozzáadjuk a felhasználót a Dictionary-hoz
                                    users.Add(username, password);
                                    usersLoaded++; // Növeljük a számlálót
                                    Console.WriteLine($" - Felhasználó hozzáadva: '{username}'");
                                }
                                else
                                {
                                    Console.WriteLine($"Figyelmeztetés: Ismétlődő felhasználónév a fájlban: {username}"); 
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Figyelmeztetés: Üres felhasználónév vagy jelszó a sorban: '{line}'");
                            }
                        }
                        
                        else if (!string.IsNullOrWhiteSpace(line))
                        {
                            Console.WriteLine($"Figyelmeztetés: Hibás sor formátum a felhasználói fájlban: '{line}'");
                        }
                    }
                    Console.WriteLine($"Felhasználók betöltése befejeződött. Összes betöltött felhasználó: {usersLoaded}"); 
                }
                else
                {
                 
                    Console.WriteLine($"Figyelmeztetés: A felhasználói fájl ({usersFilePath}) NEM található itt: {fullPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"HIBA történt a felhasználói fájl betöltésekor: {ex.Message}");
                Console.WriteLine($"Hiba típusa: {ex.GetType().Name}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            }

            Console.WriteLine($"AuthenticationManager.users dictionary mérete betöltés után: {users.Count}");
        }

        public bool AuthenticateUser(string username, string password)
        {
            Console.WriteLine($"Próbálkozó bejelentkezés: Felhasználónév='{username}', Jelszó='{password}'");
                                                                                                             
            Console.WriteLine($"AuthenticationManager.users dictionary mérete autentikációkor: {users.Count}");

            if (users.ContainsKey(username))
            {
                bool passwordMatch = users[username] == password;
                Console.WriteLine($"Felhasználó '{username}' megtalálva. Jelszó egyezés: {passwordMatch}"); 
                return passwordMatch;
            }
            else
            {
                Console.WriteLine($"Felhasználó '{username}' NEM található a users dictionary-ban.");
                return false; 
            }
        }

    }
}