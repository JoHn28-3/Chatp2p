namespace Chatp2pWPF.Models;

public class Paquet
{
    public string Type { get; set; } = "msg";   // hello, msg, file, ack, read, edit, delete, react, typing
    public string Id { get; set; } = "";
    public string De { get; set; } = "";
    public string Texte { get; set; } = "";
    public string Reponse { get; set; } = "";
    public string Reaction { get; set; } = "";
    public string NomFichier { get; set; } = "";
    public string Data { get; set; } = "";
    public bool Transfere { get; set; }
    public DateTime Date { get; set; } = DateTime.Now;
}
