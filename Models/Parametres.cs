namespace Chatp2pWPF.Models;

public class Parametres : ObjetObservable
{
    string pseudo = Environment.UserName;
    string bio = "Disponible sur ChatP2P";
    string avatarBase64 = "";
    string fond = "";
    bool sombre = true, notifSon = true, notifBureau = true,
         accusesLecture = true, indicateurSaisie = true,
         confirmSuppr = true, chiffrementActif = true;
    int port = 5000;

    public string Pseudo { get => pseudo; set { pseudo = value; Maj(); } }
    public string Bio { get => bio; set { bio = value; Maj(); } }
    public string AvatarBase64 { get => avatarBase64; set { avatarBase64 = value; Maj(); } }
    public string Fond { get => fond; set { fond = value; Maj(); } }
    public bool Sombre { get => sombre; set { sombre = value; Maj(); } }
    public bool NotifSon { get => notifSon; set { notifSon = value; Maj(); } }
    public bool NotifBureau { get => notifBureau; set { notifBureau = value; Maj(); } }
    public bool AccusesLecture { get => accusesLecture; set { accusesLecture = value; Maj(); } }
    public bool IndicateurSaisie { get => indicateurSaisie; set { indicateurSaisie = value; Maj(); } }
    public bool ConfirmSuppr { get => confirmSuppr; set { confirmSuppr = value; Maj(); } }
    public bool ChiffrementActif { get => chiffrementActif; set { chiffrementActif = value; Maj(); } }
    public int Port { get => port; set { port = value; Maj(); } }
}
