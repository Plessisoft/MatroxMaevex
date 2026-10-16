namespace MatroxMaevex;

/// <summary>
/// Table des joins de l'EISC, vue depuis le programme SIMPL.
/// "Entrée" = entrée du symbole EISC côté SIMPL (SIMPL -> C#).
/// "Sortie" = sortie du symbole EISC côté SIMPL (C# -> SIMPL).
/// </summary>
public static class Joins
{
    // Entrées digitales
    public const uint SendRequest = 1;       // impulsion : envoie la requête

    // Entrées serials (lues au moment de l'impulsion)
    public const uint Host = 1;              // ex. 192.168.1.100 (https:// par défaut, ou URL complète http://...)
    public const uint Login = 2;             // ex. JohnDoe
    public const uint Password = 3;          // ex. 123456
    public const uint Method = 4;            // GET, POST, PUT, DELETE (GET par défaut)
    public const uint Path = 5;              // ex. /api/usb/msd1/eject
    public const uint Body = 6;              // ex. {"Id":"msd1"} (vide = pas de corps)

    // Sorties digitales
    public const uint Busy = 1;              // haut pendant la requête
    public const uint Success = 2;           // impulsion : HTTP 2xx et Result.IsSuccess != false
    public const uint Error = 3;             // impulsion : échec

    // Sorties analogiques
    public const uint StatusCode = 1;        // code HTTP (0 si pas de réponse)

    // Sorties serials
    public const uint Response = 1;          // corps de la réponse
    public const uint ErrorMessage = 2;      // message d'erreur (vide si succès)
}
