using System.Globalization;

namespace GestionLocative.Core.Import;

/// <summary>Lecture souple des valeurs d'un export Excel / WinDev (dates, heures, montants, nombres, oui/non).</summary>
public static class ValeursCsv
{
    private static readonly string[] FormatsDate =
    [
        "dd/MM/yyyy", "d/M/yyyy", "dd/MM/yy", "d/M/yy",
        "dd-MM-yyyy", "d-M-yyyy", "dd.MM.yyyy", "d.M.yyyy",
        "yyyy-MM-dd", "yyyy-M-d", "yyyy/MM/dd",
        "yyyyMMdd", // format interne WinDev
    ];

    /// <summary>
    /// Date avec heure facultative : « 05/12/2026 », « 05/12/2026 15:30 », « 2026-12-05T15:30:00 », « 20261205 ».
    /// Une heure « 00:00 » (date seule exportée par Excel) est traitée comme absente.
    /// </summary>
    public static bool LireDate(string? texte, out DateOnly date, out TimeOnly? heure)
    {
        date = default;
        heure = null;
        if (string.IsNullOrWhiteSpace(texte)) return false;

        var t = texte.Trim();
        var separation = t.IndexOfAny([' ', 'T']);
        var partieDate = separation < 0 ? t : t[..separation];
        var partieHeure = separation < 0 ? null : t[(separation + 1)..].Trim();

        // WinDev : date et heure collées « 20261205153000 ».
        if (separation < 0 && t.Length is 12 or 14 && t.All(char.IsAsciiDigit))
        {
            partieDate = t[..8];
            partieHeure = $"{t[8..10]}:{t[10..12]}";
        }

        if (!DateOnly.TryParseExact(partieDate, FormatsDate, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            return false;
        if (string.IsNullOrEmpty(partieHeure)) return true;
        if (!LireHeure(partieHeure, out var h)) return false;
        heure = h == TimeOnly.MinValue ? null : h;
        return true;
    }

    /// <summary>« 16:00 », « 16h00 », « 16h », « 16 », « 9:30 », « 15:30:00 », « 1530 » (WinDev).</summary>
    public static bool LireHeure(string? texte, out TimeOnly heure)
    {
        heure = default;
        if (string.IsNullOrWhiteSpace(texte)) return false;
        var t = texte.Trim().ToLowerInvariant().Replace('h', ':').Replace('.', ':');
        if (t.Length == 4 && t.All(char.IsAsciiDigit)) t = $"{t[..2]}:{t[2..]}";
        if (t.EndsWith(':')) t += "00";
        if (!t.Contains(':')) t += ":00";
        var morceaux = t.Split(':');
        if (morceaux.Length is < 2 or > 3) return false;
        if (!int.TryParse(morceaux[0], NumberStyles.None, CultureInfo.InvariantCulture, out var h)
            || !int.TryParse(morceaux[1], NumberStyles.None, CultureInfo.InvariantCulture, out var m)
            || h is < 0 or > 23 || m is < 0 or > 59)
            return false;
        if (morceaux.Length == 3 && !int.TryParse(morceaux[2], NumberStyles.None, CultureInfo.InvariantCulture, out _))
            return false;
        heure = new TimeOnly(h, m);
        return true;
    }

    /// <summary>
    /// « 450 », « 450,50 », « 450.5 », « 1 234,50 € », « 1.234,50 », « 1,234.50 », « 450,499999 » (arrondi au centime).
    /// </summary>
    public static decimal? LireMontant(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte)) return null;
        var t = new string(texte.Where(c => c is not (' ' or ' ' or ' ' or '€' or '\'')).ToArray());
        if (t.EndsWith("eur", StringComparison.OrdinalIgnoreCase)) t = t[..^3];

        var virgule = t.LastIndexOf(',');
        var point = t.LastIndexOf('.');
        if (virgule >= 0 && point >= 0)
        {
            // Le dernier des deux sépare les décimales, l'autre les milliers.
            var milliers = virgule > point ? '.' : ',';
            t = t.Replace(milliers.ToString(), "").Replace(',', '.');
        }
        else t = t.Replace(',', '.');

        if (!decimal.TryParse(t, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var montant))
            return null;
        return decimal.Round(montant, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Nombre entier de 0 à 100 ; vide = 0. « 2,0 » est accepté (export Excel).</summary>
    public static int? LireNombre(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte)) return 0;
        var t = texte.Trim();
        if (t.EndsWith(",0") || t.EndsWith(".0")) t = t[..^2];
        return int.TryParse(t, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n <= 100 ? n : null;
    }

    /// <summary>
    /// Oui / non : « oui », « o », « 1 », « vrai », « x », « yes »... ou tout texte contenant « airbnb »
    /// (colonne « Origine » ou « Plateforme » = « Airbnb »). Vide ou autre texte (« Booking », « Direct ») = non.
    /// </summary>
    public static bool LireAirbnb(string? texte)
    {
        if (string.IsNullOrWhiteSpace(texte)) return false;
        var t = CorrespondanceColonnes.Simplifier(texte);
        return t.Contains("airbnb") || t is "oui" or "o" or "1" or "vrai" or "x" or "yes" or "y" or "true";
    }
}
