# wpf-application

Ez a projekt egy **C# WPF alapú chatprogram**, amely tartalmazza a kliens és a szerver részt is. A program lehetővé teszi, hogy több felhasználó valós időben kommunikáljon egymással.

## Funkciók

1. **Alapvető chatfunkciók**  
   - Több kliens képes kapcsolódni a szerverhez hiba nélkül.
   - Üzenetek küldése és fogadása a kliensek között, **ékezetes karakterek támogatásával**.
   - Kliens oldali lecsatlakozás a szerverről biztonságosan.

2. **Bejelentkezés / Felhasználókezelés**  
   - A kliensek csatlakozáskor **felhasználónév és jelszó párost adnak meg**.
   - Felhasználói információk kezelése és karbantartása a programban implementálva.

3. **Privát üzenetek**  
   - A felhasználók képesek **privát üzenetet küldeni** egy másik, kiválasztott felhasználónak a chat szobában.
   - Csak a kijelölt felhasználó kapja meg a privát üzenetet.

## Technológiák

- **C#** – programozási nyelv
- **WPF** – felhasználói felület
- **Sockets** – hálózati kommunikáció
