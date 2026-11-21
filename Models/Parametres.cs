namespace Chatp2pWPF.Models;

public class Parametres
{
    public string Pseudo { get; set; } = Environment.UserName;
    public bool Sombre { get; set; }
    public bool NotifSon { get; set; } = true;
    public bool AccusesLecture { get; set; } = true;
    public bool IndicateurSaisie { get; set; } = true;
    public string Fond { get; set; } = "";
}
