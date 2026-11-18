using System.Collections.ObjectModel;
using System.IO;

namespace Chatp2pWPF.Models;

public class Peer : ObjetObservable
{
    string apercu = "";
    int nonLus;
    bool epingle, muet, archive, bloque, enLigne, ecrit, ephemere;
    DateTime derniere = DateTime.MinValue;

    public string Nom { get; set; } = "";
    public ObservableCollection<ChatMessage> Messages { get; } = new();
    public StreamWriter? Writer { get; set; }
    public SemaphoreSlim Verrou { get; } = new(1, 1);
    public int JetonEcrit { get; set; }

    public string Initiale => Nom.Length > 0 ? Nom[0].ToString().ToUpper() : "?";
    public string Apercu { get => apercu; set { apercu = value; Maj(); } }
    public DateTime Derniere { get => derniere; set { derniere = value; Maj(); } }
    public int NonLus { get => nonLus; set { nonLus = value; Maj(); Maj(nameof(NonLusTexte)); Maj(nameof(HasNonLus)); } }
    public string NonLusTexte => nonLus > 0 ? nonLus.ToString() : "";
    public bool HasNonLus => nonLus > 0;
    public bool Epingle { get => epingle; set { epingle = value; Maj(); Maj(nameof(Icones)); } }
    public bool Muet { get => muet; set { muet = value; Maj(); Maj(nameof(Icones)); } }
    public bool Bloque { get => bloque; set { bloque = value; Maj(); Maj(nameof(Icones)); } }
    public bool Archive { get => archive; set { archive = value; Maj(); } }
    public bool Ephemere { get => ephemere; set { ephemere = value; Maj(); } }
    public bool EnLigne { get => enLigne; set { enLigne = value; Maj(); Maj(nameof(StatutTexte)); } }
    public bool Ecrit { get => ecrit; set { ecrit = value; Maj(); Maj(nameof(StatutTexte)); } }
    public string StatutTexte => Ecrit ? "écrit…" : EnLigne ? "en ligne" : "hors ligne";
    public string Icones => (Epingle ? "📌" : "") + (Muet ? "🔇" : "") + (Bloque ? "⛔" : "");
}